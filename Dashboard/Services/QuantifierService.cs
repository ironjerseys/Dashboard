using Dashboard.Data;
using Dashboard.DTO;
using Dashboard.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Services;

public interface IQuantifierService
{
    Task<List<QuantifierCardDto>> GetDashboardAsync(string userId, int historyDays = 7);
    Task<int> CreateWithFirstEntryAsync(string userId, string name, DateOnly date, int value);
    Task UpsertEntryAsync(string userId, int quantifierId, DateOnly date, int value);
}


public class QuantifierService : IQuantifierService
{
    private readonly IDbContextFactory<BlogContext> _dbFactory;

    public QuantifierService(IDbContextFactory<BlogContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<QuantifierCardDto>> GetDashboardAsync(string userId, int historyDays = 7)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var quantifiers = await db.Set<Quantifier>()
            .AsNoTracking()
            .Where(q => q.UserId == userId)
            .OrderBy(q => q.Name)
            .Select(q => new { q.Id, q.Name })
            .ToListAsync();

        if (quantifiers.Count == 0)
            return new List<QuantifierCardDto>();

        var ids = quantifiers.Select(x => x.Id).ToArray();
        var from = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-historyDays + 1));

        // entries récentes pour tous les quantifiers en une requête
        var entries = await db.Set<QuantifierEntry>()
            .AsNoTracking()
            .Where(e => ids.Contains(e.QuantifierId) && e.Date >= from)
            .OrderByDescending(e => e.Date)
            .Select(e => new { e.QuantifierId, e.Date, e.Value })
            .ToListAsync();

        var grouped = entries
            .GroupBy(e => e.QuantifierId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => new QuantifierEntryDto(x.Date, x.Value)).ToList()
            );

        return quantifiers.Select(q =>
            new QuantifierCardDto(
                q.Id,
                q.Name,
                grouped.TryGetValue(q.Id, out var hist) ? hist : new List<QuantifierEntryDto>()
            )
        ).ToList();
    }

    public async Task<int> CreateWithFirstEntryAsync(string userId, string name, DateOnly date, int value)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var q = new Quantifier
        {
            UserId = userId,
            Name = name.Trim()
        };

        q.Entries.Add(new QuantifierEntry
        {
            Date = date,
            Value = value,
            UpdatedAtUtc = DateTime.UtcNow
        });

        db.Add(q);
        await db.SaveChangesAsync();
        return q.Id;
    }

    public async Task UpsertEntryAsync(string userId, int quantifierId, DateOnly date, int value)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        // sécurité : s'assurer que le quantifier appartient bien au user
        var owns = await db.Set<Quantifier>()
            .AnyAsync(q => q.Id == quantifierId && q.UserId == userId);

        if (!owns)
            return;

        var existing = await db.Set<QuantifierEntry>()
            .FirstOrDefaultAsync(e => e.QuantifierId == quantifierId && e.Date == date);

        if (existing is null)
        {
            db.Add(new QuantifierEntry
            {
                QuantifierId = quantifierId,
                Date = date,
                Value = value,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            existing.Value = value;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }
}

