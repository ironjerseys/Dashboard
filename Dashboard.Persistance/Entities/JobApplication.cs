using System.ComponentModel.DataAnnotations;

namespace Dashboard.Persistance.Entities;

public enum JobApplicationStatus
{
    Applied,
    Assessment,
    Interview,
    Offer,
    Rejected
}

/// <summary>
/// Une candidature, construite a partir des mails analyses. Les mails rattaches
/// (<see cref="Emails"/>) en racontent l'historique ; le statut est celui du plus recent.
/// </summary>
public class JobApplication
{
    public int Id { get; set; }

    [Required, MaxLength(256)]
    public string Company { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Position { get; set; }

    [MaxLength(256)]
    public string? Location { get; set; }

    /// <summary>Lien vers l'offre. Sert aussi de cle pour qu'un second import du meme fichier ne duplique rien.</summary>
    [MaxLength(2048)]
    public string? JobUrl { get; set; }

    public JobApplicationStatus Status { get; set; } = JobApplicationStatus.Applied;

    /// <summary>Date du mail de confirmation ; null si la candidature n'est connue que par une reponse.</summary>
    public DateTime? AppliedUtc { get; set; }

    /// <summary>Date du mail le plus recent rattache. Sert aussi a ignorer un mail plus ancien arrive en retard.</summary>
    public DateTime LastEventUtc { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public List<EmailMessage> Emails { get; set; } = new();
}
