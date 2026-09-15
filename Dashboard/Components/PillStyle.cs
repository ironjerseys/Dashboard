using Dashboard.Persistance.Entities;

namespace Dashboard.Components;

/// <summary>Apparence d'un statut de candidature ou d'un type de mail : classe CSS, icone et libelles.</summary>
public sealed record PillStyle(string Css, string Icon, string Fr, string En)
{
    public static PillStyle For(JobApplicationStatus status) => status switch
    {
        JobApplicationStatus.Assessment => new("assessment", "bi-clipboard-check", "Test", "Assessment"),
        JobApplicationStatus.Interview => new("interview", "bi-people", "Entretien", "Interview"),
        JobApplicationStatus.Offer => new("offer", "bi-trophy", "Offre", "Offer"),
        JobApplicationStatus.Rejected => new("rejected", "bi-x-circle", "Refusée", "Rejected"),
        _ => new("applied", "bi-send", "Envoyée", "Applied")
    };

    public static PillStyle For(EmailEventType eventType) => eventType switch
    {
        EmailEventType.ApplicationReceived => new("applied", "bi-send", "Confirmation", "Confirmation"),
        EmailEventType.Assessment => new("assessment", "bi-clipboard-check", "Test", "Assessment"),
        EmailEventType.Interview => new("interview", "bi-people", "Entretien", "Interview"),
        EmailEventType.Offer => new("offer", "bi-trophy", "Offre", "Offer"),
        EmailEventType.Rejection => new("rejected", "bi-x-circle", "Refus", "Rejection"),
        EmailEventType.OtherUpdate => new("muted", "bi-info-circle", "Mise à jour", "Update"),
        _ => new("muted", "bi-slash-circle", "Hors candidature", "Not an application")
    };
}
