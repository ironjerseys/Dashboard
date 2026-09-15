using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dashboard.Business;

public sealed record LabelPushResult(int Applied, int Failed);

public interface IMailLabelService
{
    /// <summary>
    /// Pose le libelle Processed sur les mails analyses qui ne l'ont pas encore. Ce qui echoue
    /// reste en attente et sera retente au passage suivant.
    /// </summary>
    Task<LabelPushResult> PushPendingLabelsAsync(CancellationToken cancellationToken = default);
}

public sealed class MailLabelService : IMailLabelService
{
    /// <summary>Libelle systeme Gmail de la boite de reception ; le retirer revient a archiver.</summary>
    private const string InboxLabel = "\\Inbox";

    private const int MaxChangesPerPush = 500;

    private readonly IDbContextFactory<BlogContext> _dbContextFactory;
    private readonly IMailSource _mailSource;
    private readonly MailOptions _options;

    public MailLabelService(
        IDbContextFactory<BlogContext> dbContextFactory,
        IMailSource mailSource,
        IOptions<MailOptions> options)
    {
        _dbContextFactory = dbContextFactory;
        _mailSource = mailSource;
        _options = options.Value;
    }

    public async Task<LabelPushResult> PushPendingLabelsAsync(CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var pending = await dbContext.EmailMessages
            .AsNoTracking()
            .Where(m => m.LabelSyncPending)
            .OrderBy(m => m.Id)
            .Take(MaxChangesPerPush)
            .Select(m => new { m.Id, m.MessageId, m.Uid, m.UidValidity, m.Folder })
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return new LabelPushResult(0, 0);
        }

        IReadOnlyList<string> add = [_options.ProcessedLabel];
        IReadOnlyList<string> remove = _options.ArchiveProcessed ? [InboxLabel] : [];

        var outcomes = new List<MailLabelOutcome>(pending.Count);

        foreach (var folderGroup in pending.GroupBy(m => m.Folder))
        {
            List<MailLabelChange> changes = folderGroup
                .Select(m => new MailLabelChange(m.Id, m.MessageId, m.Uid, m.UidValidity, add, remove))
                .ToList();

            try
            {
                outcomes.AddRange(await _mailSource.ApplyLabelsAsync(folderGroup.Key, changes, cancellationToken));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Connexion ou dossier en echec : tout le lot reste en attente avec la raison.
                outcomes.AddRange(changes.Select(c => new MailLabelOutcome(c.Key, ex.Message)));
            }
        }

        foreach (var group in outcomes.GroupBy(o => o.Error))
        {
            List<int> ids = group.Select(o => o.Key).ToList();
            string? error = Truncate(group.Key, 1024);
            bool stillPending = error is not null;

            await dbContext.EmailMessages
                .Where(m => ids.Contains(m.Id))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.LabelSyncPending, stillPending)
                    .SetProperty(m => m.LabelSyncError, error),
                    cancellationToken);
        }

        var result = new LabelPushResult(
            Applied: outcomes.Count(o => o.Error is null),
            Failed: outcomes.Count(o => o.Error is not null));

        dbContext.Logs.Add(new Log
        {
            Level = result.Failed > 0 ? "Warning" : "Info",
            Source = nameof(MailLabelService),
            Event = "PushLabels",
            Message = $"Applied={result.Applied}; Failed={result.Failed}; " +
                      $"FirstError={outcomes.FirstOrDefault(o => o.Error is not null)?.Error}",
            TimestampUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
