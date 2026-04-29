using Dashboard.Persistance.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Services;

public record JobCountItem(string Label, int Count);

public class JobStats
{
    public int TotalCount { get; set; }
    public int RemoteCount { get; set; }
    public DateTime? LastScrapeAt { get; set; }
    public List<JobCountItem> ByRole { get; set; } = [];
    public List<JobCountItem> ByCity { get; set; } = [];
    public List<JobCountItem> BySite { get; set; } = [];
    public List<JobCountItem> ByWeek { get; set; } = [];
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

        // Regroupement par semaine (lundi de chaque semaine de scraping)
        var byWeek = await jobs
            .GroupBy(j => new
            {
                Year = j.ScrapedAt.Year,
                // Numéro de semaine ISO approximé : jour de l'année / 7
                Week = (j.ScrapedAt.DayOfYear - 1) / 7
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Week,
                Count = g.Count(),
                MinDate = g.Min(j => j.ScrapedAt)
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Week)
            .ToListAsync(ct);

        return new JobStats
        {
            TotalCount = total,
            RemoteCount = remote,
            LastScrapeAt = lastScrape,
            ByRole = byRole.Select(x => new JobCountItem(x.Label, x.Count)).ToList(),
            ByCity = byCity.Select(x => new JobCountItem(x.Label, x.Count)).ToList(),
            BySite = bySite.Select(x => new JobCountItem(x.Label, x.Count)).ToList(),
            ByWeek = byWeek
                .Select(x => new JobCountItem(x.MinDate.ToString("dd/MM/yy"), x.Count))
                .ToList(),
        };
    }
}
