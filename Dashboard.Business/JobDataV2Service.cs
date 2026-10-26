using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
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

// Dernier run, pour un poste : offres renvoyées par les sites et offres dont le titre correspond.
public record JobRoleRelevance(string Role, int RawCount, int MatchedCount, bool IsFiltered)
{
    public int Percent => RawCount == 0 ? 0 : (int)Math.Round(MatchedCount * 100.0 / RawCount);
}

public record JobTitleCount(string Title, int Count);

public class JobStatsV2
{
    // Vrai : filtre de titre + doublons entre sites retirés. Faux : résultats bruts des sites.
    public bool Reliable { get; set; }

    public List<JobRunV2Info> Runs { get; set; } = [];        // du plus ancien au plus récent
    public JobRunV2Info? Latest { get; set; }

    // Photo du dernier run
    public int LatestRemoteCount { get; set; }
    public List<JobCountItem> LatestByRole { get; set; } = [];
    public List<JobCountItem> LatestByCity { get; set; } = [];
    public List<JobCountItem> LatestBySite { get; set; } = [];
    public List<JobRoleCityItem> LatestByRoleCity { get; set; } = [];
    public List<JobSearchV2Issue> LatestIssues { get; set; } = [];

    // Pertinence par poste (toujours calculée, quel que soit le mode) et doublons retirés.
    public List<JobRoleRelevance> LatestRelevance { get; set; } = [];
    public int LatestDuplicatesRemoved { get; set; }

    // Évolution, run par run
    public List<JobRunV2RoleCount> TrendByRole { get; set; } = [];
    public List<JobRunV2RoleCityCount> TrendByRoleCity { get; set; } = [];
    public List<JobRunV2RoleCount> NewByRole { get; set; } = [];

    // Date du premier run de chaque poste (son premier run n'a pas de "nouvelles" offres).
    public Dictionary<string, DateTime> RoleFirstRunUtc { get; set; } = new();
}

public interface IJobDataV2Service
{
    Task<JobStatsV2> GetStatsAsync(bool reliable = true, CancellationToken ct = default);

    Task<List<JobRoleFilterV2>> GetFiltersAsync(CancellationToken ct = default);
    Task SaveFilterAsync(string role, string includeTerms, string? excludeTerms, CancellationToken ct = default);

    // Titres des offres trouvées par ce poste lors du dernier run (pour régler son filtre).
    Task<List<JobTitleCount>> GetLatestTitlesAsync(string role, CancellationToken ct = default);
}

// Lecture du système Job Data V2 (tables *V2, alimentées par jobspy-scraper/scrape_jobs.py).
//
// Mode fiabilisé (par défaut) :
// - une offre ne compte pour un poste que si son titre passe le filtre du poste (JobRoleFiltersV2) ;
// - la même offre publiée sur LinkedIn et Indeed ne compte qu'une fois : deux offres avec le même
//   titre et la même entreprise, dans la même ville de recherche, sont une seule offre.
// Les filtres s'appliquent à la lecture : rien n'est supprimé en base, et ils valent aussi pour
// l'historique. Les comptes sont toujours en offres distinctes.
public sealed class JobDataV2Service : IJobDataV2Service
{
    private readonly IDbContextFactory<BlogContext> _factory;

    public JobDataV2Service(IDbContextFactory<BlogContext> factory)
    {
        _factory = factory;
    }

    private readonly record struct Row(int RunId, string Role, string City, string Site, int PostingId, int Key, bool Remote, bool Matched);

    public async Task<JobStatsV2> GetStatsAsync(bool reliable = true, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var runs = await db.JobScrapeRunsV2.AsNoTracking()
            .Where(r => r.Status == "Completed")
            .OrderBy(r => r.StartedUtc)
            .Select(r => new { r.Id, r.StartedUtc, r.IsBackfill })
            .ToListAsync(ct);

        var stats = new JobStatsV2 { Reliable = reliable };
        if (runs.Count == 0)
            return stats;

        var searches = await db.JobScrapeSearchesV2.AsNoTracking()
            .Where(s => s.Run!.Status == "Completed")
            .Select(s => new { s.Id, s.RunId, s.Role, s.City, s.Site, s.Succeeded, s.IsCapped })
            .ToListAsync(ct);
        var searchById = searches.ToDictionary(s => s.Id);

        var sightings = await db.JobSightingsV2.AsNoTracking()
            .Where(s => s.Search!.Run!.Status == "Completed")
            .Select(s => new { s.SearchId, s.JobPostingId })
            .ToListAsync(ct);

        var postings = await db.JobPostingsV2.AsNoTracking()
            .Select(p => new { p.Id, p.Title, p.Company, p.IsRemote })
            .ToDictionaryAsync(p => p.Id, ct);

        var matchers = await LoadMatchersAsync(db, ct);

        // ----- Une ligne par (recherche, offre), avec la clé de comptage -----
        var normTitle = new Dictionary<int, string>();
        var matchCache = new Dictionary<(string, int), bool>();
        var keyIds = new Dictionary<string, int>();
        var rows = new List<Row>(sightings.Count);

        foreach (var si in sightings)
        {
            var search = searchById[si.SearchId];
            if (!postings.TryGetValue(si.JobPostingId, out var p)) continue;

            if (!matchCache.TryGetValue((search.Role, p.Id), out var matched))
            {
                matched = !matchers.TryGetValue(search.Role, out var m) || m.Matches(p.Title);
                matchCache[(search.Role, p.Id)] = matched;
            }

            int key = p.Id;
            if (reliable)
            {
                if (!normTitle.TryGetValue(p.Id, out var nt))
                    normTitle[p.Id] = nt = JobTitleMatcher.Normalize(p.Title) + "|" + JobTitleMatcher.Normalize(p.Company);
                var k = nt + "|" + search.City;
                if (!keyIds.TryGetValue(k, out key))
                    keyIds[k] = key = keyIds.Count;
            }

            rows.Add(new Row(search.RunId, search.Role, search.City, search.Site, p.Id, key, p.IsRemote == true, matched));
        }

        var kept = reliable ? rows.Where(r => r.Matched).ToList() : rows;
        static int Distinct(IEnumerable<Row> rs) => rs.Select(r => r.Key).Distinct().Count();

        // ----- Runs -----
        var searchCounts = searches.GroupBy(s => s.RunId).ToDictionary(g => g.Key, g => new
        {
            Total = g.Count(),
            Failed = g.Count(s => !s.Succeeded),
            Capped = g.Count(s => s.IsCapped)
        });
        var offerCounts = kept.GroupBy(r => r.RunId).ToDictionary(g => g.Key, Distinct);

        // Premier run (par date) où chaque offre a été vue. Par date et non par Id : les runs
        // reconstitués sont créés après coup.
        var startById = runs.ToDictionary(r => r.Id, r => r.StartedUtc);
        var runIdByStart = runs.GroupBy(r => r.StartedUtc).ToDictionary(g => g.Key, g => g.First().Id);
        var newByRun = kept
            .GroupBy(r => r.Key)
            .Select(g => g.Min(r => startById[r.RunId]))
            .GroupBy(d => runIdByStart[d])
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

        // ----- Photo du dernier run -----
        stats.Latest = stats.Runs[^1];
        int latestId = stats.Latest.Id;
        var latest = kept.Where(r => r.RunId == latestId).ToList();

        stats.LatestRemoteCount = Distinct(latest.Where(r => r.Remote));
        stats.LatestByRole = CountBy(latest, r => r.Role);
        stats.LatestByCity = CountBy(latest, r => r.City);
        stats.LatestBySite = CountBy(latest, r => r.Site);
        stats.LatestByRoleCity = latest
            .GroupBy(r => (r.Role, r.City))
            .Select(g => new JobRoleCityItem(g.Key.Role, g.Key.City, Distinct(g)))
            .ToList();

        var latestAll = rows.Where(r => r.RunId == latestId).ToList();
        stats.LatestRelevance = latestAll
            .GroupBy(r => r.Role)
            .Select(g => new JobRoleRelevance(
                g.Key,
                g.Select(r => r.PostingId).Distinct().Count(),
                g.Where(r => r.Matched).Select(r => r.PostingId).Distinct().Count(),
                matchers.ContainsKey(g.Key)))
            .OrderByDescending(x => x.MatchedCount)
            .ToList();
        if (reliable)
            stats.LatestDuplicatesRemoved = latest.Select(r => r.PostingId).Distinct().Count() - Distinct(latest);

        stats.LatestIssues = await db.JobScrapeSearchesV2.AsNoTracking()
            .Where(s => s.RunId == latestId && (!s.Succeeded || s.IsCapped))
            .OrderBy(s => s.Role).ThenBy(s => s.City).ThenBy(s => s.Site)
            .Select(s => new JobSearchV2Issue(s.Role, s.City, s.Site, s.ResultCount, s.Succeeded, s.IsCapped, s.Error))
            .ToListAsync(ct);

        // ----- Évolution -----
        stats.TrendByRole = kept
            .GroupBy(r => (r.RunId, r.Role))
            .Select(g => new JobRunV2RoleCount(g.Key.RunId, g.Key.Role, Distinct(g)))
            .ToList();

        stats.TrendByRoleCity = kept
            .GroupBy(r => (r.RunId, r.Role, r.City))
            .Select(g => new JobRunV2RoleCityCount(g.Key.RunId, g.Key.Role, g.Key.City, Distinct(g)))
            .ToList();

        // Nouvelles offres par run et par poste : une offre est "nouvelle" pour un poste lors du
        // premier run où ce poste l'a trouvée. Le premier run de chaque poste ramasse d'un coup
        // tout son stock : ce ne sont pas des "nouvelles" offres, ce run est exclu.
        var roleFirstRun = searches
            .GroupBy(s => s.Role)
            .ToDictionary(g => g.Key, g => g.Min(s => startById[s.RunId]));
        stats.RoleFirstRunUtc = roleFirstRun;

        stats.NewByRole = kept
            .GroupBy(r => (r.Key, r.Role))
            .Select(g => (g.Key.Role, First: g.Min(r => startById[r.RunId])))
            .Where(x => x.First != roleFirstRun[x.Role])
            .GroupBy(x => (RunId: runIdByStart[x.First], x.Role))
            .Select(g => new JobRunV2RoleCount(g.Key.RunId, g.Key.Role, g.Count()))
            .ToList();

        return stats;
    }

    private static List<JobCountItem> CountBy(IEnumerable<Row> rows, Func<Row, string> label) =>
        rows.GroupBy(label)
            .Select(g => new JobCountItem(g.Key, g.Select(r => r.Key).Distinct().Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

    private static async Task<Dictionary<string, JobTitleMatcher>> LoadMatchersAsync(BlogContext db, CancellationToken ct)
    {
        var filters = await db.JobRoleFiltersV2.AsNoTracking().ToListAsync(ct);
        return filters
            .Select(f => (f.Role, Matcher: new JobTitleMatcher(f.IncludeTerms, f.ExcludeTerms)))
            .Where(x => x.Matcher.IsFiltered)
            .ToDictionary(x => x.Role, x => x.Matcher, StringComparer.OrdinalIgnoreCase);
    }

    // ----- Filtres de titre -----

    public async Task<List<JobRoleFilterV2>> GetFiltersAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.JobRoleFiltersV2.AsNoTracking().OrderBy(f => f.Role).ToListAsync(ct);
    }

    public async Task SaveFilterAsync(string role, string includeTerms, string? excludeTerms, CancellationToken ct = default)
    {
        role = role.Trim();
        var include = string.Join("\n", JobTitleMatcher.SplitTerms(includeTerms));
        var exclude = string.Join("\n", JobTitleMatcher.SplitTerms(excludeTerms));

        await using var db = await _factory.CreateDbContextAsync(ct);
        var filter = await db.JobRoleFiltersV2.FirstOrDefaultAsync(f => f.Role == role, ct);

        // Aucun terme inclus = plus de filtre pour ce poste.
        if (include.Length == 0)
        {
            if (filter is not null)
            {
                db.JobRoleFiltersV2.Remove(filter);
                await db.SaveChangesAsync(ct);
            }
            return;
        }

        if (filter is null)
        {
            filter = new JobRoleFilterV2 { Role = role };
            db.JobRoleFiltersV2.Add(filter);
        }
        filter.IncludeTerms = include;
        filter.ExcludeTerms = exclude.Length == 0 ? null : exclude;
        filter.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<JobTitleCount>> GetLatestTitlesAsync(string role, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var latestId = await db.JobScrapeSearchesV2.AsNoTracking()
            .Where(s => s.Role == role && s.Run!.Status == "Completed")
            .OrderByDescending(s => s.Run!.StartedUtc)
            .Select(s => (int?)s.RunId)
            .FirstOrDefaultAsync(ct);
        if (latestId is null) return [];

        var titles = await db.JobSightingsV2.AsNoTracking()
            .Where(s => s.Search!.RunId == latestId && s.Search.Role == role)
            .Select(s => new { s.JobPostingId, s.JobPosting!.Title })
            .Distinct()
            .ToListAsync(ct);

        return titles
            .GroupBy(t => t.Title.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new JobTitleCount(g.First().Title.Trim(), g.Count()))
            .OrderByDescending(t => t.Count).ThenBy(t => t.Title)
            .ToList();
    }
}
