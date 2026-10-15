using Dashboard.Persistance.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Business;

public record JobCountItem(string Label, int Count);
public record JobRoleCityItem(string Role, string City, int Count);
/// Offres inserees par un run de scraping (regroupe par jour), pour un couple rôle × ville.
public record JobRunItem(string Role, string City, DateTime RunDate, int Count);

public class JobStats
{
    public int TotalCount { get; set; }
    public int RemoteCount { get; set; }
    public DateTime? LastScrapeAt { get; set; }
    public List<JobCountItem> ByRole { get; set; } = [];
    public List<JobCountItem> ByCity { get; set; } = [];
    public List<JobCountItem> BySite { get; set; } = [];
    public List<JobRoleCityItem> ByRoleCity { get; set; } = [];
    public List<JobRunItem> ByRun { get; set; } = [];
}

public interface IJobPostingService
{
    Task<JobStats> GetStatsAsync(CancellationToken ct = default);
}

public sealed class JobPostingService : IJobPostingService
{
    private readonly IDbContextFactory<BlogContext> _factory;

    public JobPostingService(IDbContextFactory<BlogContext> factory)
    {
        _factory = factory;
    }

    public async Task<JobStats> GetStatsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var jobs = db.JobPostings.AsNoTracking();

        var total = await jobs.CountAsync(ct);
        var remote = await jobs.CountAsync(j => j.IsRemote == true, ct);
        var lastScrape = await jobs.MaxAsync(j => (DateTime?)j.ScrapedAt, ct);

        var byRole = await jobs
            .GroupBy(j => j.SearchRole)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct);

        var byCity = await jobs
            .GroupBy(j => j.SearchCity)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct);

        var bySite = await jobs
            .GroupBy(j => j.Site)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct);

        // Chaque run insere ses offres avec le meme ScrapedAt : on regroupe par jour de run.
        var byRun = await jobs
            .GroupBy(j => new { j.SearchRole, j.SearchCity, RunDate = j.ScrapedAt.Date })
            .Select(g => new { g.Key.SearchRole, g.Key.SearchCity, g.Key.RunDate, Count = g.Count() })
            .OrderBy(x => x.RunDate)
            .ToListAsync(ct);

        var byRoleCity = await jobs
            .GroupBy(j => new { j.SearchRole, j.SearchCity })
            .Select(g => new { Role = g.Key.SearchRole, City = g.Key.SearchCity, Count = g.Count() })
            .ToListAsync(ct);

        return new JobStats
        {
            TotalCount = total,
            RemoteCount = remote,
            LastScrapeAt = lastScrape,
            ByRole = byRole.Select(x => new JobCountItem(x.Label, x.Count)).ToList(),
            ByCity = byCity.Select(x => new JobCountItem(x.Label, x.Count)).ToList(),
            BySite = bySite.Select(x => new JobCountItem(x.Label, x.Count)).ToList(),
            ByRoleCity = byRoleCity.Select(x => new JobRoleCityItem(x.Role, x.City, x.Count)).ToList(),
            ByRun = byRun.Select(x => new JobRunItem(x.SearchRole, x.SearchCity, x.RunDate, x.Count)).ToList(),
        };
    }
}
