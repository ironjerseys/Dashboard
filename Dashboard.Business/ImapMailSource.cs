using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Dashboard.Business;

/// <summary>
/// Recuperation des mails par IMAP (MailKit). Connexion en lecture seule : ce service ne
/// marque rien comme lu, ne deplace rien, ne supprime rien.
/// </summary>
public sealed class ImapMailSource : IMailSource
{
    private readonly MailOptions _options;

    public ImapMailSource(IOptions<MailOptions> options)
    {
        _options = options.Value;
    }

    public async Task<MailFetchResult> FetchAsync(
        string folder,
        long knownUidValidity,
        long lastSeenUid,
        int maxMessages,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.UserName) || string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new InvalidOperationException(
                "Mail:UserName et Mail:Password doivent etre configures (User Secrets ou variables d'environnement).");
        }

        using var client = new ImapClient();

        await client.ConnectAsync(
            _options.Host,
            _options.Port,
            _options.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls,
            cancellationToken);

        await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);

        try
        {
            IMailFolder mailFolder = await client.GetFolderAsync(folder, cancellationToken);
            await mailFolder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

            long uidValidity = mailFolder.UidValidity;

            // Si le serveur a change d'epoque, les UID connus ne veulent plus rien dire.
            bool cursorUsable = knownUidValidity == uidValidity && lastSeenUid > 0;

            IList<UniqueId> uids = cursorUsable
                ? await mailFolder.SearchAsync(
                    new UniqueIdRange(new UniqueId((uint)lastSeenUid + 1), UniqueId.MaxValue),
                    SearchQuery.All,
                    cancellationToken)
                : await mailFolder.SearchAsync(
                    SearchQuery.DeliveredAfter(DateTime.UtcNow.AddDays(-_options.InitialLookbackDays)),
                    cancellationToken);

            // Les plus recents d'abord, puis plafond : un premier passage sur une grosse boite
            // ne doit pas tout rapatrier d'un coup.
            List<UniqueId> selected = uids
                .OrderByDescending(u => u.Id)
                .Take(maxMessages)
                .OrderBy(u => u.Id)
                .ToList();

            var messages = new List<RawEmail>(selected.Count);

            foreach (UniqueId uid in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();

                MimeMessage message = await mailFolder.GetMessageAsync(uid, cancellationToken);
                messages.Add(ToRawEmail(message, uid, uidValidity, folder));
            }

            return new MailFetchResult(uidValidity, messages);
        }
        finally
        {
            await client.DisconnectAsync(true, CancellationToken.None);
        }
    }

    private static RawEmail ToRawEmail(MimeMessage message, UniqueId uid, long uidValidity, string folder)
    {
        MailboxAddress? sender = message.From.Mailboxes.FirstOrDefault();

        // Certains expediteurs omettent le Message-ID : on en fabrique un stable a partir
        // des coordonnees IMAP, ce qui preserve la deduplication.
        string messageId = string.IsNullOrWhiteSpace(message.MessageId)
            ? $"imap:{folder}:{uidValidity}:{uid.Id}"
            : message.MessageId;

        return new RawEmail(
            MessageId: Truncate(messageId, 512),
            Uid: uid.Id,
            FromAddress: Truncate(sender?.Address, 320) ?? "",
            FromName: Truncate(sender?.Name, 256),
            Subject: Truncate(message.Subject, 512),
            SentUtc: message.Date.UtcDateTime,
            TextBody: message.TextBody,
            HtmlBody: message.HtmlBody);
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
