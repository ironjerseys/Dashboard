using Dashboard.Data;
using Dashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Services;

public interface IGoalService
{
    Task<List<Goal>> GetMyGoalsAsync(string userId);
    Task<Goal?> GetAsync(int id, string userId);
    Task<int> CreateAsync(Goal goal);
    Task<bool> ToggleDoneAsync(int id, string userId);
    Task<bool> UpdateArticleAsync(int id, string userId, int? articleId);
    Task<Dictionary<DateOnly, bool>> GetMonthlyCoverageMapAsync(string userId, int year, int month);
}

public record GoalProgress(int Done, int Target, bool Met);

public class GoalService : IGoalService
{
    private readonly BlogContext _db;
    public GoalService(BlogContext db) => _db = db;

    public async Task<List<Goal>> GetMyGoalsAsync(string userId)
        => await _db.Goals.Where(g => g.OwnerId == userId).Include(g => g.Article).OrderBy(g => g.Debut).ToListAsync();

    public Task<Goal?> GetAsync(int id, string userId)
        => _db.Goals.Include(g => g.Article).FirstOrDefaultAsync(g => g.Id == id && g.OwnerId == userId);

    public async Task<int> CreateAsync(Goal goal)
    {
        if (goal.Fin < goal.Debut)
            throw new ArgumentException("Fin doit être >= Début");
        goal.IsDone = false; // default
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

    public async Task<Dictionary<DateOnly, bool>> GetMonthlyCoverageMapAsync(string userId, int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var goals = await _db.Goals.Where(g => g.OwnerId == userId && g.Fin >= first && g.Debut <= last).ToListAsync();
        var map = new Dictionary<DateOnly, bool>();
        for (int d = 1; d <= last.Day; d++)
        {
            var date = new DateOnly(year, month, d);
            var covering = goals.Where(g => g.Debut <= date && g.Fin >= date).ToList();
            if (covering.Count == 0) continue; // pas d'objectif ce jour => pas de couleur
            bool green = covering.All(g => g.IsDone);
            map[date] = green;
        }
        return map;
    }
}
