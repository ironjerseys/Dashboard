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

    // ---- Analyse automatique ----

    public EmailAnalysisState AnalysisState { get; set; } = EmailAnalysisState.Pending;

    /// <summary>Tentatives facturables ou non ; plafonnees pour qu'un mail qui plante ne coute pas en boucle.</summary>
    public int AnalysisAttempts { get; set; }

    public DateTime? AnalyzedUtc { get; set; }

    [MaxLength(1024)]
    public string? AnalysisError { get; set; }

    public EmailEventType? EventType { get; set; }

    [MaxLength(256)]
    public string? ExtractedCompany { get; set; }

    [MaxLength(256)]
    public string? ExtractedPosition { get; set; }

    [MaxLength(256)]
    public string? ExtractedLocation { get; set; }

    [MaxLength(512)]
    public string? AnalysisSummary { get; set; }

    /// <summary>Le rattachement automatique a hesite (plusieurs candidatures possibles) : a trancher a la main.</summary>
    public bool NeedsReview { get; set; }

    public int? JobApplicationId { get; set; }
    public JobApplication? JobApplication { get; set; }

    // ---- Reflet dans Gmail ----

    /// <summary>
    /// Le mail a ete analyse mais le libelle Gmail n'est pas encore pose. Reste a true
    /// tant que Gmail n'a pas accepte la modification : le passage suivant la retente.
    /// </summary>
    public bool LabelSyncPending { get; set; }

    /// <summary>Derniere erreur rencontree en alignant les libelles Gmail.</summary>
    [MaxLength(1024)]
    public string? LabelSyncError { get; set; }
}
