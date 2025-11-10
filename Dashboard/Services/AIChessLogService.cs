using Dashboard.Data;
using Dashboard.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Services;

public interface IAIChessLogService
{
    Task<int> AddAsync(AIChessLogs entry);
    Task<List<AIChessLogs>> GetRecentAsync(int take = 200);
}

public class AIChessLogService : IAIChessLogService
{
    private readonly BlogContext _db;
    public AIChessLogService(BlogContext db) => _db = db;

    public async Task<int> AddAsync(AIChessLogs entry)
    {
        _db.AIChessLogs.Add(entry);
        await _db.SaveChangesAsync();
        return entry.Id;
    }

    public Task<List<AIChessLogs>> GetRecentAsync(int take = 200)
        => _db.AIChessLogs.OrderByDescending(l => l.TimestampUtc).Take(take).ToListAsync();
}
