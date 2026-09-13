namespace Dashboard.Business;

/// <summary>Un mail tel que rendu par la source, avant tout stockage ou interpretation.</summary>
public sealed record RawEmail(
    string MessageId,
    long Uid,
    string FromAddress,
    string? FromName,
    string? Subject,
    DateTime SentUtc,
    string? TextBody,
    string? HtmlBody);

/// <summary>
/// Resultat d'un passage. <paramref name="UidValidity"/> est renvoyee telle que le serveur
/// l'annonce : si elle differe de celle connue, le curseur d'UID doit etre remis a zero.
/// </summary>
public sealed record MailFetchResult(long UidValidity, IReadOnlyList<RawEmail> Messages);

/// <summary>
/// Source de mails. L'implementation IMAP est la seule aujourd'hui ; passer a l'API Gmail
/// plus tard ne demanderait qu'une seconde implementation de cette interface.
/// </summary>
public interface IMailSource
{
    /// <param name="knownUidValidity">UidValidity connue, ou 0 si aucun passage n'a encore eu lieu.</param>
    /// <param name="lastSeenUid">Dernier UID traite, ou 0 pour un premier passage.</param>
    Task<MailFetchResult> FetchAsync(
        string folder,
        long knownUidValidity,
        long lastSeenUid,
        int maxMessages,
        CancellationToken cancellationToken = default);
}
