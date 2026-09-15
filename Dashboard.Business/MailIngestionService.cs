using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dashboard.Business;

public sealed record MailIngestionResult(
    int Fetched,
    int Inserted,
    int Duplicates,
    long UidValidity,
    long LastSeenUid,
    bool CursorWasReset,
    int Remaining);

public interface IMailIngestionService
{
    /// <summary>Rapatrie les nouveaux mails du dossier configure et les stocke tels quels.</summary>
    Task<MailIngestionResult> SyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Efface le curseur pour que le prochain passage reparte de la recherche par date
    /// (InitialLookbackDays) au lieu de la plage d'UID. Les mails deja stockes ne bougent pas :
    /// le Message-ID les protege du doublon.
    /// </summary>
    Task ResetCursorAsync(CancellationToken cancellationToken = default);
}

public sealed class MailIngestionService : IMailIngestionService
{
    private readonly IDbContextFactory<BlogContext> _dbContextFactory;
    private readonly IMailSource _mailSource;
    private readonly MailOptions _options;

    public MailIngestionService(
        IDbContextFactory<BlogContext> dbContextFactory,
        IMailSource mailSource,
        IOptions<MailOptions> options)
    {
        _dbContextFactory = dbContextFactory;
        _mailSource = mailSource;
        _options = options.Value;
    }

    public async Task ResetCursorAsync(CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var states = await dbContext.MailboxSyncStates
            .Where(s => s.Account == _options.UserName && s.Folder == _options.Folder)
            .ToListAsync(cancellationToken);

        dbContext.MailboxSyncStates.RemoveRange(states);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<MailIngestionResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        string account = _options.UserName;
        string folder = _options.Folder;

        MailboxSyncState? state = await dbContext.MailboxSyncStates
            .FirstOrDefaultAsync(s => s.Account == account && s.Folder == folder, cancellationToken);

        MailFetchResult fetch = await _mailSource.FetchAsync(
            folder,
            state?.UidValidity ?? 0,
            state?.LastSeenUid ?? 0,
            _options.MaxMessagesPerRun,
            cancellationToken);

        bool cursorWasReset = state is not null && state.UidValidity != fetch.UidValidity;

        // Deduplication intra-lot d'abord : deux copies du meme Message-ID dans un meme
        // passage feraient sauter l'index unique au SaveChanges.
        List<RawEmail> batch = fetch.Messages
            .GroupBy(m => m.MessageId)
            .Select(g => g.First())
            .ToList();

        var incomingIds = batch.Select(m => m.MessageId).ToList();

        HashSet<string> known = (await dbContext.EmailMessages
                .AsNoTracking()
                .Where(m => incomingIds.Contains(m.MessageId))
                .Select(m => m.MessageId)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var toInsert = batch
            .Where(m => !known.Contains(m.MessageId))
            .Select(m => new EmailMessage
            {
                MessageId = m.MessageId,
                Uid = m.Uid,
                UidValidity = fetch.UidValidity,
                Folder = folder,
                FromAddress = m.FromAddress,
                FromName = m.FromName,
                Subject = m.Subject,
                SentUtc = m.SentUtc,
                TextBody = m.TextBody,
                HtmlBody = m.HtmlBody,
                IngestedUtc = DateTime.UtcNow
            })
            .ToList();

        if (toInsert.Count > 0)
        {
            dbContext.EmailMessages.AddRange(toInsert);
        }

        // Le curseur avance meme sur les doublons : ces UID ont bien ete examines.
        // On prend le lot brut, pas le dedoublonne : la copie ecartee peut porter l'UID le plus haut.
        long highestUid = fetch.Messages.Count > 0 ? fetch.Messages.Max(m => m.Uid) : (state?.LastSeenUid ?? 0);

        if (state is null)
        {
            state = new MailboxSyncState { Account = account, Folder = folder };
            dbContext.MailboxSyncStates.Add(state);
        }

        state.UidValidity = fetch.UidValidity;
        state.LastSeenUid = Math.Max(cursorWasReset ? 0 : state.LastSeenUid, highestUid);
        state.LastSyncUtc = DateTime.UtcNow;

        var result = new MailIngestionResult(
            Fetched: batch.Count,
            Inserted: toInsert.Count,
            Duplicates: batch.Count - toInsert.Count,
            UidValidity: fetch.UidValidity,
            LastSeenUid: state.LastSeenUid,
            CursorWasReset: cursorWasReset,
            Remaining: fetch.Remaining);

        dbContext.Logs.Add(new Log
        {
            Level = "Info",
            Source = nameof(MailIngestionService),
            Event = "Sync",
            Message = $"Folder={folder}; Fetched={result.Fetched}; Inserted={result.Inserted}; " +
                      $"Duplicates={result.Duplicates}; LastSeenUid={result.LastSeenUid}; Reset={cursorWasReset}; Remaining={result.Remaining}",
            TimestampUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }
}
