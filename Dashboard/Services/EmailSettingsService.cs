using Dashboard.Data;
using Dashboard.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Services;

public interface IEmailSettingsService
{
    Task<EmailSettings> GetOrCreateAsync(string userId, string defaultRecipientEmail, CancellationToken cancellationToken = default);
    Task SaveAsync(EmailSettings model, string userId, CancellationToken cancellationToken = default);
}

public sealed class EmailSettingsService : IEmailSettingsService
{
    private readonly IDbContextFactory<BlogContext> _dbContextFactory;

    public EmailSettingsService(IDbContextFactory<BlogContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<EmailSettings> GetOrCreateAsync(string userId, string defaultRecipientEmail, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        EmailSettings? existing = await dbContext.EmailSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        // Valeurs par défaut (tu peux ajuster)
        return new EmailSettings
        {
            UserId = userId,
            RecipientEmail = defaultRecipientEmail,
            Enabled = false,
            Frequency = EmailFrequency.Daily,
            Hour = 9,
            Minute = 0,
            DayOfWeek = null,
            DayOfMonth = null,
            IncludeTodos = true,
            IncludeGoals = true,
            IncludeArticles = true
        };
    }

    public async Task SaveAsync(EmailSettings model, string userId, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        EmailSettings? existing = await dbContext.EmailSettings
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        // On ne fait jamais confiance au UserId posté depuis le client
        model.UserId = userId;

        NormalizeByFrequency(model);

        if (existing is null)
        {
            dbContext.EmailSettings.Add(model);
        }
        else
        {
            existing.Enabled = model.Enabled;
            existing.Frequency = model.Frequency;
            existing.Hour = model.Hour;
            existing.Minute = model.Minute;
            existing.DayOfWeek = model.DayOfWeek;
            existing.DayOfMonth = model.DayOfMonth;
            existing.RecipientEmail = model.RecipientEmail;
            existing.IncludeTodos = model.IncludeTodos;
            existing.IncludeGoals = model.IncludeGoals;
            existing.IncludeArticles = model.IncludeArticles;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void NormalizeByFrequency(EmailSettings model)
    {
        // Nettoyage simple pour éviter des combinaisons incohérentes
        if (model.Frequency == EmailFrequency.Daily)
        {
            model.DayOfWeek = null;
            model.DayOfMonth = null;
        }
        else if (model.Frequency == EmailFrequency.Weekly)
        {
            model.DayOfMonth = null;
        }
        else if (model.Frequency == EmailFrequency.Monthly)
        {
            model.DayOfWeek = null;
        }
    }
}
