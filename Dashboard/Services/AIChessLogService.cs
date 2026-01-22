using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Services;

public interface IAIChessLogService
{
    Task<int> AddAsync(AIChessLogs entry, CancellationToken cancellationToken = default);
    Task<List<AIChessLogs>> GetRecentAsync(int take = 200, CancellationToken cancellationToken = default);
}

public sealed class AIChessLogService : IAIChessLogService
{
    private readonly IDbContextFactory<BlogContext> _dbContextFactory;

    public AIChessLogService(IDbContextFactory<BlogContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<int> AddAsync(AIChessLogs entry, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.AIChessLogs.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        return entry.Id;
    }

    public async Task<List<AIChessLogs>> GetRecentAsync(int take = 200, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.AIChessLogs
            .AsNoTracking()
            .OrderByDescending(log => log.TimestampUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
