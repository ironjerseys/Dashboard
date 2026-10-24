namespace Dashboard.Persistance.Entities;

/// Offre d'apprenti mecanicien au Quebec, alimentee par jobspy-scraper/scrape_mechanics.py.
public class MechanicJobPosting
{
    public int Id { get; set; }

    /// "guichet-emplois", "quebec-emploi" ou "indeed".
    public string Source { get; set; } = "";
    public string? ExternalId { get; set; }
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Company { get; set; }
    public string? City { get; set; }
    public DateTime? DatePosted { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Salary { get; set; }
    public string? SearchTerm { get; set; }

    /// Premier et dernier run de scraping ou l'offre a ete vue (UTC).
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}
