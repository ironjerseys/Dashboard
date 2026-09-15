using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Business;

public interface IEmailMessageService
{
    /// <summary>Le mail complet, corps compris.</summary>
    Task<EmailMessage?> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Remet un mail en echec dans la file d'analyse. Action manuelle : elle peut couter un appel.</summary>
    Task RetryAnalysisAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class EmailMessageService : IEmailMessageService
{
    private readonly IDbContextFactory<BlogContext> _dbContextFactory;

    public EmailMessageService(IDbContextFactory<BlogContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<EmailMessage?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.EmailMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task RetryAnalysisAsync(int id, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.EmailMessages
            .Where(m => m.Id == id && m.AnalysisState == EmailAnalysisState.Failed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.AnalysisState, EmailAnalysisState.Pending)
                .SetProperty(m => m.AnalysisAttempts, 0)
                .SetProperty(m => m.AnalysisError, (string?)null),
                cancellationToken);
    }
}
