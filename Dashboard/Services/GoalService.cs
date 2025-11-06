using Dashboard.Data;
using Dashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Services;

public interface IGoalService
{
    Task<List<Goal>> GetMyGoalsAsync(string userId, DateOnly? weekStart = null);
    Task<Goal?> GetAsync(int id, string userId);
    Task<int> CreateAsync(Goal goal);
    Task<bool> ToggleDoneAsync(int id, string userId);
    Task<bool> UpdateArticleAsync(int id, string userId, int? articleId);
    Task<Dictionary<DateOnly, bool>> GetMonthlyWeekMapAsync(string userId, int year, int month);
}

public record GoalProgress(int Done, int Target, bool Met);

public class GoalService : IGoalService
{
    private readonly BlogContext _db;

    public GoalService(BlogContext db) => _db = db;

    public async Task<List<Goal>> GetMyGoalsAsync(string userId, DateOnly? weekStart = null)
    {
        var q = _db.Goals.AsQueryable().Where(g => g.OwnerId == userId);
        if (weekStart.HasValue) q = q.Where(g => g.WeekStart == weekStart.Value);
        return await q.Include(g => g.Article).OrderBy(g => g.Titre).ToListAsync();
    }

    public Task<Goal?> GetAsync(int id, string userId)
        => _db.Goals.Include(g => g.Article).FirstOrDefaultAsync(g => g.Id == id && g.OwnerId == userId);

    public async Task<int> CreateAsync(Goal goal)
    {
        // Normalize to Monday week start
        goal.WeekStart = NormalizeToMonday(goal.WeekStart);
        _db.Goals.Add(goal);
        await _db.SaveChangesAsync();
        return goal.Id;
    }

    public async Task<bool> ToggleDoneAsync(int id, string userId)
    {
        var goal = await _db.Goals.FirstOrDefaultAsync(g => g.Id == id && g.OwnerId == userId);
        if (goal == null) return false;
        goal.IsDone = !goal.IsDone;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateArticleAsync(int id, string userId, int? articleId)
    {
        var goal = await _db.Goals.FirstOrDefaultAsync(g => g.Id == id && g.OwnerId == userId);
        if (goal == null) return false;
        goal.ArticleId = articleId;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<Dictionary<DateOnly, bool>> GetMonthlyWeekMapAsync(string userId, int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        var days = DateTime.DaysInMonth(year, month);
        var map = new Dictionary<DateOnly, bool>(days);
        for (int d = 1; d <= days; d++)
        {
            var date = new DateOnly(year, month, d);
            var week = NormalizeToMonday(date);
            var met = await _db.Goals.AnyAsync(g => g.OwnerId == userId && g.WeekStart == week && g.IsDone);
            map[date] = met;
        }
        return map;
    }

    private static DateOnly NormalizeToMonday(DateOnly date)
    {
        int delta = (int)date.DayOfWeek - (int)DayOfWeek.Monday;
        if (delta < 0) delta += 7;
        return date.AddDays(-delta);
    }
}
