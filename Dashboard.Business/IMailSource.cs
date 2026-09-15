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
/// <paramref name="Remaining"/> compte les mails laisses de cote par le plafond : un autre
/// passage les recuperera.
/// </summary>
public sealed record MailFetchResult(long UidValidity, IReadOnlyList<RawEmail> Messages, int Remaining);

/// <summary>
/// Libelles a poser et a retirer sur un mail deja stocke. <paramref name="Key"/> est l'identifiant
/// cote appelant, renvoye tel quel dans <see cref="MailLabelOutcome"/>. L'UID n'est fiable que si
/// l'UidValidity du dossier n'a pas change ; sinon on retrouve le mail par son Message-ID.
/// </summary>
public sealed record MailLabelChange(
    int Key,
    string MessageId,
    long Uid,
    long UidValidity,
    IReadOnlyList<string> Add,
    IReadOnlyList<string> Remove);

/// <summary><paramref name="Error"/> vaut null si Gmail a accepte la modification.</summary>
public sealed record MailLabelOutcome(int Key, string? Error);

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

    /// <summary>
    /// Pose et retire des libelles Gmail sur des mails du dossier. Seule operation en ecriture :
    /// rien n'est jamais supprime. Renvoie un resultat par changement demande.
    /// </summary>
    Task<IReadOnlyList<MailLabelOutcome>> ApplyLabelsAsync(
        string folder,
        IReadOnlyList<MailLabelChange> changes,
        CancellationToken cancellationToken = default);
}
