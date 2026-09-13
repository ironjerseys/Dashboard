using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Business;

public interface IReminderSettingsService
{
    /// <summary>Preferences de l'utilisateur, ou les valeurs par defaut s'il n'a jamais rien enregistre.</summary>
    Task<ReminderSetting> GetAsync(string ownerId, CancellationToken cancellationToken = default);

    /// <summary>Cree ou met a jour la ligne de l'utilisateur.</summary>
    Task SaveAsync(string ownerId, bool enabled, int hourLocal, string? emailOverride, CancellationToken cancellationToken = default);

    /// <summary>Toutes les preferences enregistrees, indexees par OwnerId. Utilise par ReminderService.</summary>
    Task<Dictionary<string, ReminderSetting>> GetAllAsync(CancellationToken cancellationToken = default);
}

public sealed class ReminderSettingsService : IReminderSettingsService
{
    private readonly IDbContextFactory<BlogContext> _dbContextFactory;

    public ReminderSettingsService(IDbContextFactory<BlogContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<ReminderSetting> GetAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        ReminderSetting? existing = await dbContext.ReminderSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OwnerId == ownerId, cancellationToken);

        // Pas de ligne => on renvoie les valeurs par defaut sans rien ecrire en base.
        return existing ?? new ReminderSetting { OwnerId = ownerId };
    }

    public async Task SaveAsync(string ownerId, bool enabled, int hourLocal, string? emailOverride, CancellationToken cancellationToken = default)
    {
        if (hourLocal is < 0 or > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(hourLocal), "Hour must be between 0 and 23.");
        }

        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        ReminderSetting? existing = await dbContext.ReminderSettings
            .FirstOrDefaultAsync(r => r.OwnerId == ownerId, cancellationToken);

        if (existing is null)
        {
            existing = new ReminderSetting { OwnerId = ownerId };
            dbContext.ReminderSettings.Add(existing);
        }

        existing.Enabled = enabled;
        existing.HourLocal = hourLocal;
        existing.EmailOverride = string.IsNullOrWhiteSpace(emailOverride) ? null : emailOverride.Trim();
        existing.UpdatedUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Dictionary<string, ReminderSetting>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.ReminderSettings
            .AsNoTracking()
            .ToDictionaryAsync(r => r.OwnerId, cancellationToken);
    }
}
