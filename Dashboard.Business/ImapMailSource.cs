using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Dashboard.Business;

/// <summary>
/// Acces a la boite par IMAP (MailKit). La recuperation ouvre le dossier en lecture seule ;
/// seule la pose de libelles l'ouvre en ecriture. Rien n'est jamais deplace ni supprime.
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
        using ImapClient client = await ConnectAsync(cancellationToken);

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

            // Les plus anciens d'abord, puis plafond : le curseur avance jusqu'au dernier UID du
            // lot et le passage suivant reprend juste apres. Prendre les plus recents ferait
            // sauter le curseur par-dessus tout ce qui depasse le plafond, perdu pour de bon.
            // En IMAP, "n:*" renvoie toujours au moins le dernier mail, meme quand son UID est
            // inferieur a n : sans ce filtre, chaque passage retelechargeait le dernier mail.
            if (cursorUsable)
            {
                uids = uids.Where(u => u.Id > lastSeenUid).ToList();
            }

            List<UniqueId> selected = uids
                .OrderBy(u => u.Id)
                .Take(maxMessages)
                .ToList();

            int remaining = uids.Count - selected.Count;

            var messages = new List<RawEmail>(selected.Count);

            foreach (UniqueId uid in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();

                MimeMessage message = await mailFolder.GetMessageAsync(uid, cancellationToken);
                messages.Add(ToRawEmail(message, uid, uidValidity, folder));
            }

            return new MailFetchResult(uidValidity, messages, remaining);
        }
        finally
        {
            await client.DisconnectAsync(true, CancellationToken.None);
        }
    }

    public async Task<IReadOnlyList<MailLabelOutcome>> ApplyLabelsAsync(
        string folder,
        IReadOnlyList<MailLabelChange> changes,
        CancellationToken cancellationToken = default)
    {
        if (changes.Count == 0)
        {
            return [];
        }

        using ImapClient client = await ConnectAsync(cancellationToken);

        try
        {
            if (!client.Capabilities.HasFlag(ImapCapabilities.GMailExt1))
            {
                throw new InvalidOperationException(
                    "Le serveur IMAP ne gere pas les libelles Gmail (extension X-GM-EXT1).");
            }

            IMailFolder mailFolder = await client.GetFolderAsync(folder, cancellationToken);
            await mailFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

            var outcomes = new List<MailLabelOutcome>(changes.Count);
            var located = new List<(MailLabelChange Change, UniqueId Uid)>(changes.Count);

            // UID encore valides : on verifie qu'ils sont toujours dans le dossier. Un mail dont
            // on a retire le libelle a la main n'y est plus, et un STORE dessus passerait sous silence.
            var byUid = changes.Where(c => c.UidValidity == mailFolder.UidValidity).ToList();

            if (byUid.Count > 0)
            {
                IList<UniqueId> requested = byUid.Select(c => new UniqueId((uint)c.Uid)).ToList();
                HashSet<uint> present = (await mailFolder.SearchAsync(SearchQuery.Uids(requested), cancellationToken))
                    .Select(u => u.Id)
                    .ToHashSet();

                foreach (MailLabelChange change in byUid)
                {
                    if (present.Contains((uint)change.Uid))
                    {
                        located.Add((change, new UniqueId((uint)change.Uid)));
                    }
                    else
                    {
                        outcomes.Add(new MailLabelOutcome(change.Key, NotFound(folder)));
                    }
                }
            }

            // UidValidity changee : les UID stockes sont caducs, on repasse par le Message-ID.
            foreach (MailLabelChange change in changes.Where(c => c.UidValidity != mailFolder.UidValidity))
            {
                if (change.MessageId.StartsWith("imap:", StringComparison.Ordinal))
                {
                    outcomes.Add(new MailLabelOutcome(change.Key,
                        "Mail sans Message-ID et UidValidity changee : impossible de le retrouver."));
                    continue;
                }

                IList<UniqueId> found = await mailFolder.SearchAsync(
                    SearchQuery.HeaderContains("Message-ID", change.MessageId),
                    cancellationToken);

                if (found.Count == 0)
                {
                    outcomes.Add(new MailLabelOutcome(change.Key, NotFound(folder)));
                }
                else
                {
                    located.AddRange(found.Select(uid => (change, uid)));
                }
            }

            // Un aller-retour par combinaison de libelles plutot qu'un par mail.
            var groups = located.GroupBy(l =>
                string.Join('', l.Change.Add) + '' + string.Join('', l.Change.Remove));

            foreach (var group in groups)
            {
                MailLabelChange sample = group.First().Change;
                List<UniqueId> uids = group.Select(l => l.Uid).Distinct().ToList();
                List<int> keys = group.Select(l => l.Change.Key).Distinct().ToList();

                try
                {
                    if (sample.Add.Count > 0)
                    {
                        await mailFolder.StoreAsync(uids,
                            new StoreLabelsRequest(StoreAction.Add, sample.Add) { Silent = true },
                            cancellationToken);
                    }

                    if (sample.Remove.Count > 0)
                    {
                        await mailFolder.StoreAsync(uids,
                            new StoreLabelsRequest(StoreAction.Remove, sample.Remove) { Silent = true },
                            cancellationToken);
                    }

                    outcomes.AddRange(keys.Select(k => new MailLabelOutcome(k, null)));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    outcomes.AddRange(keys.Select(k => new MailLabelOutcome(k, ex.Message)));
                }
            }

            return outcomes;
        }
        finally
        {
            await client.DisconnectAsync(true, CancellationToken.None);
        }
    }

    private async Task<ImapClient> ConnectAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.UserName) || string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new InvalidOperationException(
                "Mail:UserName et Mail:Password doivent etre configures (User Secrets ou variables d'environnement).");
        }

        var client = new ImapClient();

        try
        {
            await client.ConnectAsync(
                _options.Host,
                _options.Port,
                _options.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls,
                cancellationToken);

            await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);

            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static string NotFound(string folder) =>
        $"Introuvable dans le dossier {folder} (libelle retire a la main ?).";

    private static RawEmail ToRawEmail(MimeMessage message, UniqueId uid, long uidValidity, string folder)
    {
        MailboxAddress? sender = message.From.Mailboxes.FirstOrDefault();

        // Certains expediteurs omettent le Message-ID : on en fabrique un stable a partir
        // des coordonnees IMAP, ce qui preserve la deduplication.
        string messageId = string.IsNullOrWhiteSpace(message.MessageId)
            ? $"imap:{folder}:{uidValidity}:{uid.Id}"
            : message.MessageId;

        return new RawEmail(
            MessageId: Truncate(messageId, 512)!,
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
