using System.ComponentModel.DataAnnotations;

namespace Dashboard.Persistance.Entities;

// Poste recherché par le scraper d'offres (jobspy-scraper/scrape_jobs.py lit cette table).
// Ajout uniquement depuis l'interface : les offres déjà collectées gardent ce SearchTerm
// dans JobPostings.SearchRole, le supprimer casserait l'historique des graphes.
public class ScraperRole
{
    public int Id { get; set; }

    // Terme envoyé à LinkedIn / Indeed / Glassdoor, ex : "Product Owner".
    [Required, MaxLength(128)]
    public string SearchTerm { get; set; } = string.Empty;

    // Début de la recherche Google Jobs, complété par le scraper avec "<ville> since last month".
    // Null = "<SearchTerm> jobs near".
    [MaxLength(256)]
    public string? GooglePhrase { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
