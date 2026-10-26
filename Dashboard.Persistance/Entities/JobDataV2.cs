namespace Dashboard.Persistance.Entities;

// ── Job Data V2 ──
// Système parallèle à JobPostings : au lieu de ne garder que les nouvelles offres, on garde
// la "photo" complète de chaque run (quelles offres étaient en ligne lors de ce run, et pour
// quelle recherche). Alimenté par jobspy-scraper/scrape_jobs.py, lu par la page /job-data-v2.

// Un run du scraper.
public class JobScrapeRunV2
{
    public int Id { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime? FinishedUtc { get; set; }

    // Running, Completed, Failed.
    public string Status { get; set; } = "Running";

    // Run reconstitué à partir de l'historique de JobPostings (script de reprise), pas un vrai run.
    public bool IsBackfill { get; set; }

    // Fenêtre de recherche (offres publiées depuis N heures) et plafond de résultats par site.
    public int HoursOld { get; set; }
    public int ResultsPerSite { get; set; }

    public List<JobScrapeSearchV2> Searches { get; set; } = new();
}

// Une recherche d'un run : un poste, dans une ville, sur un site.
public class JobScrapeSearchV2
{
    public int Id { get; set; }
    public int RunId { get; set; }
    public JobScrapeRunV2? Run { get; set; }

    public string Role { get; set; } = "";
    public string City { get; set; } = "";
    public string Site { get; set; } = "";

    public int ResultCount { get; set; }

    // Faux si le site a renvoyé une erreur (blocage 429, code HTTP, exception) : le résultat
    // est partiel et ne doit pas être lu comme une baisse du marché.
    public bool Succeeded { get; set; }

    // Vrai si ResultCount a atteint le plafond : le vrai nombre d'offres est peut-être plus grand.
    public bool IsCapped { get; set; }

    public string? Error { get; set; }
}

// Une offre unique (par URL), toutes recherches confondues.
public class JobPostingV2
{
    public int Id { get; set; }
    public string Site { get; set; } = "";
    public string JobUrl { get; set; } = "";
    public string? JobUrlDirect { get; set; }
    public string Title { get; set; } = "";
    public string? Company { get; set; }
    public string? Location { get; set; }
    public DateTime? DatePosted { get; set; }
    public string? JobType { get; set; }
    public string? Interval { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public string? Currency { get; set; }
    public bool? IsRemote { get; set; }
    public string? JobLevel { get; set; }
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
}

// "Cette offre a été trouvée par cette recherche." Une offre trouvée par deux recherches
// (ex : Business analyst et Analyste fonctionnel) compte pour les deux postes.
public class JobSightingV2
{
    public int SearchId { get; set; }
    public JobScrapeSearchV2? Search { get; set; }

    public int JobPostingId { get; set; }
    public JobPostingV2? JobPosting { get; set; }
}
