using Dashboard.Persistance.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Business;

// Un run du système V2, avec ses compteurs.
public record JobRunV2Info(
    int Id,
    DateTime StartedUtc,
    bool IsBackfill,
    int OfferCount,          // offres distinctes vues lors de ce run (la "photo")
    int NewOfferCount,       // offres vues pour la première fois lors de ce run
    double? DaysSincePrevious,
    int SearchCount,
    int FailedSearchCount,
    int CappedSearchCount)
{
    public bool IsIncomplete => FailedSearchCount > 0;

    // Nouvelles offres par jour écoulé depuis le run précédent : comparable quel que soit l'écart entre runs.
    public double? NewOffersPerDay => DaysSincePrevious is > 0 ? NewOfferCount / DaysSincePrevious : null;
}

public record JobRunV2RoleCount(int RunId, string Role, int Count);
public record JobRunV2RoleCityCount(int RunId, string Role, string City, int Count);
public record JobSearchV2Issue(string Role, string City, string Site, int ResultCount, bool Succeeded, bool IsCapped, string? Error);

public class JobStatsV2
{
    public List<JobRunV2Info> Runs { get; set; } = [];        // du plus ancien au plus récent
    public JobRunV2Info? Latest { get; set; }

    // Photo du dernier run
    public int LatestRemoteCount { get; set; }
    public List<JobCountItem> LatestByRole { get; set; } = [];
    public List<JobCountItem> LatestByCity { get; set; } = [];
    public List<JobCountItem> LatestBySite { get; set; } = [];
    public List<JobRoleCityItem> LatestByRoleCity { get; set; } = [];
    public List<JobSearchV2Issue> LatestIssues { get; set; } = [];

    // Évolution, run par run
    public List<JobRunV2RoleCount> TrendByRole { get; set; } = [];
    public List<JobRunV2RoleCityCount> TrendByRoleCity { get; set; } = [];
    public List<JobRunV2RoleCount> NewByRole { get; set; } = [];

    // Date du premier run de chaque poste (son premier run n'a pas de "nouvelles" offres).
    public Dictionary<string, DateTime> RoleFirstRunUtc { get; set; } = new();
}

public interface IJobDataV2Service
{
    Task<JobStatsV2> GetStatsAsync(CancellationToken ct = default);
}

// Lecture du système Job Data V2 (tables *V2, alimentées par jobspy-scraper/scrape_jobs.py).
// Toutes les comptes sont en offres distinctes : une offre trouvée par deux recherches d'un même
// groupe ne compte qu'une fois dans ce groupe.
public sealed class JobDataV2Service : IJobDataV2Service
{
    private readonly IDbContextFactory<BlogContext> _factory;

    public JobDataV2Service(IDbContextFactory<BlogContext> factory)
    {
        _factory = factory;
    }

    public async Task<JobStatsV2> GetStatsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var runs = await db.JobScrapeRunsV2.AsNoTracking()
            .Where(r => r.Status == "Completed")
            .OrderBy(r => r.StartedUtc)
            .Select(r => new { r.Id, r.StartedUtc, r.IsBackfill })
            .ToListAsync(ct);

        var stats = new JobStatsV2();
        if (runs.Count == 0)
            return stats;

        var runIds = runs.Select(r => r.Id).ToList();
        var sightings = db.JobSightingsV2.AsNoTracking().Where(s => runIds.Contains(s.Search!.RunId));

        var searchCounts = await db.JobScrapeSearchesV2.AsNoTracking()
            .Where(s => runIds.Contains(s.RunId))
            .GroupBy(s => s.RunId)
            .Select(g => new
            {
                RunId = g.Key,
                Total = g.Count(),
                Failed = g.Count(s => !s.Succeeded),
                Capped = g.Count(s => s.IsCapped)
            })
            .ToDictionaryAsync(x => x.RunId, ct);

        var offerCounts = await sightings
            .GroupBy(s => s.Search!.RunId)
            .Select(g => new { RunId = g.Key, Count = g.Select(s => s.JobPostingId).Distinct().Count() })
            .ToDictionaryAsync(x => x.RunId, x => x.Count, ct);

        // Premier run (par date) où chaque offre a été vue, avec le poste qui l'a trouvée.
        // On passe par la date et non l'Id : les runs reconstitués sont créés après coup.
        var runIdByStart = runs.GroupBy(r => r.StartedUtc).ToDictionary(g => g.Key, g => g.First().Id);
        var firstSeen = await sightings
            .GroupBy(s => s.JobPostingId)
            .Select(g => g.Min(s => s.Search!.Run!.StartedUtc))
            .ToListAsync(ct);
        var newByRun = firstSeen
            .GroupBy(d => runIdByStart.TryGetValue(d, out var id) ? id : 0)
            .ToDictionary(g => g.Key, g => g.Count());

        DateTime? previous = null;
        foreach (var r in runs)
        {
            searchCounts.TryGetValue(r.Id, out var sc);
            stats.Runs.Add(new JobRunV2Info(
                r.Id,
                r.StartedUtc,
                r.IsBackfill,
                offerCounts.GetValueOrDefault(r.Id),
                newByRun.GetValueOrDefault(r.Id),
                previous is null ? null : (r.StartedUtc - previous.Value).TotalDays,
                sc?.Total ?? 0,
                sc?.Failed ?? 0,
                sc?.Capped ?? 0));
            previous = r.StartedUtc;
        }

        stats.Latest = stats.Runs[^1];
        int latestId = stats.Latest.Id;
        var latest = sightings.Where(s => s.Search!.RunId == latestId);

        stats.LatestRemoteCount = await latest
            .Where(s => s.JobPosting!.IsRemote == true)
            .Select(s => s.JobPostingId).Distinct().CountAsync(ct);

        stats.LatestByRole = (await latest
                .GroupBy(s => s.Search!.Role)
                .Select(g => new { g.Key, Count = g.Select(s => s.JobPostingId).Distinct().Count() })
                .ToListAsync(ct))
            .OrderByDescending(x => x.Count).Select(x => new JobCountItem(x.Key, x.Count)).ToList();

        stats.LatestByCity = (await latest
                .GroupBy(s => s.Search!.City)
                .Select(g => new { g.Key, Count = g.Select(s => s.JobPostingId).Distinct().Count() })
                .ToListAsync(ct))
            .OrderByDescending(x => x.Count).Select(x => new JobCountItem(x.Key, x.Count)).ToList();

        stats.LatestBySite = (await latest
                .GroupBy(s => s.Search!.Site)
                .Select(g => new { g.Key, Count = g.Select(s => s.JobPostingId).Distinct().Count() })
                .ToListAsync(ct))
            .OrderByDescending(x => x.Count).Select(x => new JobCountItem(x.Key, x.Count)).ToList();

        stats.LatestByRoleCity = (await latest
                .GroupBy(s => new { s.Search!.Role, s.Search.City })
                .Select(g => new { g.Key.Role, g.Key.City, Count = g.Select(s => s.JobPostingId).Distinct().Count() })
                .ToListAsync(ct))
            .Select(x => new JobRoleCityItem(x.Role, x.City, x.Count)).ToList();

        stats.LatestIssues = await db.JobScrapeSearchesV2.AsNoTracking()
            .Where(s => s.RunId == latestId && (!s.Succeeded || s.IsCapped))
            .OrderBy(s => s.Role).ThenBy(s => s.City).ThenBy(s => s.Site)
            .Select(s => new JobSearchV2Issue(s.Role, s.City, s.Site, s.ResultCount, s.Succeeded, s.IsCapped, s.Error))
            .ToListAsync(ct);

        stats.TrendByRole = (await sightings
                .GroupBy(s => new { s.Search!.RunId, s.Search.Role })
                .Select(g => new { g.Key.RunId, g.Key.Role, Count = g.Select(s => s.JobPostingId).Distinct().Count() })
                .ToListAsync(ct))
            .Select(x => new JobRunV2RoleCount(x.RunId, x.Role, x.Count)).ToList();

        stats.TrendByRoleCity = (await sightings
                .GroupBy(s => new { s.Search!.RunId, s.Search.Role, s.Search.City })
                .Select(g => new { g.Key.RunId, g.Key.Role, g.Key.City, Count = g.Select(s => s.JobPostingId).Distinct().Count() })
                .ToListAsync(ct))
            .Select(x => new JobRunV2RoleCityCount(x.RunId, x.Role, x.City, x.Count)).ToList();

        // Nouvelles offres par run et par poste : une offre est "nouvelle" pour un poste lors du
        // premier run où ce poste l'a trouvée.
        var firstByRole = await sightings
            .GroupBy(s => new { s.JobPostingId, s.Search!.Role })
            .Select(g => new { g.Key.Role, First = g.Min(s => s.Search!.Run!.StartedUtc) })
            .ToListAsync(ct);
        // Premier run de chaque poste : il ramasse d'un coup tout le stock des 30 derniers jours,
        // ce ne sont pas des "nouvelles" offres. Ce run est exclu (pas de valeur dans le graphe).
        var roleFirstRun = await db.JobScrapeSearchesV2.AsNoTracking()
            .Where(s => runIds.Contains(s.RunId))
            .GroupBy(s => s.Role)
            .Select(g => new { Role = g.Key, First = g.Min(s => s.Run!.StartedUtc) })
            .ToDictionaryAsync(x => x.Role, x => x.First, ct);

        stats.RoleFirstRunUtc = roleFirstRun;
        stats.NewByRole = firstByRole
            .Where(x => !roleFirstRun.TryGetValue(x.Role, out var first) || x.First != first)
            .GroupBy(x => (RunId: runIdByStart.TryGetValue(x.First, out var id) ? id : 0, x.Role))
            .Select(g => new JobRunV2RoleCount(g.Key.RunId, g.Key.Role, g.Count()))
            .ToList();

        return stats;
    }
}
