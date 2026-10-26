using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Business;

public enum AddScraperRoleResult { Added, Empty, TooLong, AlreadyExists }

// Paramétrage de la page /job-data : postes recherchés par le scraper (ajout uniquement)
// et graphes affichés.
public interface IJobDataSettingsService
{
    Task<List<ScraperRole>> GetRolesAsync(CancellationToken ct = default);
    Task<AddScraperRoleResult> AddRoleAsync(string searchTerm, CancellationToken ct = default);
    Task<HashSet<string>> GetHiddenChartsAsync(CancellationToken ct = default);
    Task SetChartVisibleAsync(string chartKey, bool visible, CancellationToken ct = default);
}

public sealed class JobDataSettingsService : IJobDataSettingsService
{
    private const int SearchTermMaxLength = 128;

    private readonly IDbContextFactory<BlogContext> _factory;

    public JobDataSettingsService(IDbContextFactory<BlogContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<ScraperRole>> GetRolesAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.ScraperRoles.AsNoTracking().OrderBy(r => r.Id).ToListAsync(ct);
    }

    // La recherche Google est déduite du poste par le scraper ("<poste> jobs near <ville>").
    public async Task<AddScraperRoleResult> AddRoleAsync(string searchTerm, CancellationToken ct = default)
    {
        string term = (searchTerm ?? string.Empty).Trim();

        if (term.Length == 0)
            return AddScraperRoleResult.Empty;
        if (term.Length > SearchTermMaxLength)
            return AddScraperRoleResult.TooLong;

        await using var db = await _factory.CreateDbContextAsync(ct);

        // Le SearchTerm devient le nom du rôle dans les graphes : pas de doublon, même à la casse près.
        string lower = term.ToLower();
        if (await db.ScraperRoles.AnyAsync(r => r.SearchTerm.ToLower() == lower, ct))
            return AddScraperRoleResult.AlreadyExists;

        db.ScraperRoles.Add(new ScraperRole { SearchTerm = term });
        await db.SaveChangesAsync(ct);
        return AddScraperRoleResult.Added;
    }

    public async Task<HashSet<string>> GetHiddenChartsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var keys = await db.HiddenJobCharts.AsNoTracking().Select(h => h.ChartKey).ToListAsync(ct);
        return keys.ToHashSet(StringComparer.Ordinal);
    }

    public async Task SetChartVisibleAsync(string chartKey, bool visible, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        if (visible)
        {
            await db.HiddenJobCharts.Where(h => h.ChartKey == chartKey).ExecuteDeleteAsync(ct);
            return;
        }

        if (!await db.HiddenJobCharts.AnyAsync(h => h.ChartKey == chartKey, ct))
        {
            db.HiddenJobCharts.Add(new HiddenJobChart { ChartKey = chartKey });
            await db.SaveChangesAsync(ct);
        }
    }
}
