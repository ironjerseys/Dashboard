using System.ComponentModel.DataAnnotations;

namespace Dashboard.Persistance.Entities;

/// <summary>
/// Un mail recupere dans la boite, stocke brut. On ne l'interprete pas ici : le parsing
/// viendra plus tard et pourra etre rejoue autant de fois que necessaire sur ces lignes.
/// </summary>
public class EmailMessage
{
    public int Id { get; set; }

    /// <summary>
    /// En-tete Message-ID (RFC 5322). Cle de deduplication : elle est stable d'un dossier
    /// a l'autre et survit a une reinitialisation des UID IMAP.
    /// </summary>
    [Required, MaxLength(512)]
    public string MessageId { get; set; } = string.Empty;

    /// <summary>UID IMAP, unique seulement au sein d'un couple (dossier, UidValidity).</summary>
    public long Uid { get; set; }

    public long UidValidity { get; set; }

    [Required, MaxLength(128)]
    public string Folder { get; set; } = string.Empty;

    [MaxLength(320)]
    public string FromAddress { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? FromName { get; set; }

    [MaxLength(512)]
    public string? Subject { get; set; }

    /// <summary>Date de l'en-tete Date, normalisee en UTC.</summary>
    public DateTime SentUtc { get; set; }

    public string? TextBody { get; set; }

    public string? HtmlBody { get; set; }

    public DateTime IngestedUtc { get; set; } = DateTime.UtcNow;
}
