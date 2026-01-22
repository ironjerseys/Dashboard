using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Services;

public interface IGoalService
{
    Task<List<Goal>> GetMyGoalsAsync(string userId);
    Task<Goal?> GetAsync(int goalId, string userId);

    Task<int> CreateAsync(Goal goal);
    Task<bool> UpdateAsync(Goal goal, string userId);

    Task<bool> ToggleDoneAsync(int goalId, string userId);
    Task<bool> UpdateArticleAsync(int goalId, string userId, int? articleId);

    Task<bool> DeleteAsync(int goalId, string userId);

    Task<Dictionary<DateOnly, bool>> GetMonthlyCoverageMapAsync(string userId, int year, int month);
}


public class GoalService : IGoalService
{
    private readonly IDbContextFactory<BlogContext> _dbContextFactory;

    public GoalService(IDbContextFactory<BlogContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<Goal>> GetMyGoalsAsync(string userId)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.Goals
            .AsNoTracking()
            .Where(goal => goal.OwnerId == userId)
            .Include(goal => goal.Article)
            .OrderBy(goal => goal.Debut)
            .ToListAsync();
    }

    public async Task<Goal?> GetAsync(int goalId, string userId)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.Goals
            .AsNoTracking()
            .Include(goal => goal.Article)
            .FirstOrDefaultAsync(goal => goal.Id == goalId && goal.OwnerId == userId);
    }

    public async Task<int> CreateAsync(Goal goal)
    {
        ValidateGoalDates(goal.Debut, goal.Fin);

        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Normalisation minimale
        goal.Titre = (goal.Titre ?? string.Empty).Trim();
        goal.Description = (goal.Description ?? string.Empty).Trim();

        // Si tu veux forcer "non fait" à la création
        goal.IsDone = false;

        dbContext.Goals.Add(goal);
        await dbContext.SaveChangesAsync();

        return goal.Id;
    }

    public async Task<bool> UpdateAsync(Goal goal, string userId)
    {
        ValidateGoalDates(goal.Debut, goal.Fin);

        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        Goal? existingGoal = await dbContext.Goals
            .FirstOrDefaultAsync(g => g.Id == goal.Id && g.OwnerId == userId);

        if (existingGoal is null)
        {
            return false;
        }

        existingGoal.Titre = (goal.Titre ?? string.Empty).Trim();
        existingGoal.Description = (goal.Description ?? string.Empty).Trim();
        existingGoal.Debut = goal.Debut;
        existingGoal.Fin = goal.Fin;
        existingGoal.ArticleId = goal.ArticleId;

        // Autoriser la modif IsDone seulement si tu le veux
        existingGoal.IsDone = goal.IsDone;

        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleDoneAsync(int goalId, string userId)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        Goal? existingGoal = await dbContext.Goals
            .FirstOrDefaultAsync(goal => goal.Id == goalId && goal.OwnerId == userId);

        if (existingGoal is null)
        {
            return false;
        }

        existingGoal.IsDone = !existingGoal.IsDone;
        await dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<bool> UpdateArticleAsync(int goalId, string userId, int? articleId)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        Goal? existingGoal = await dbContext.Goals
            .FirstOrDefaultAsync(goal => goal.Id == goalId && goal.OwnerId == userId);

        if (existingGoal is null)
        {
            return false;
        }

        existingGoal.ArticleId = articleId;
        await dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(int goalId, string userId)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        Goal? goalToDelete = await dbContext.Goals
            .FirstOrDefaultAsync(goal => goal.Id == goalId && goal.OwnerId == userId);

        if (goalToDelete is null)
        {
            return false;
        }

        dbContext.Goals.Remove(goalToDelete);
        await dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<Dictionary<DateOnly, bool>> GetMonthlyCoverageMapAsync(string userId, int year, int month)
    {
        DateOnly firstDayOfMonth = new(year, month, 1);
        DateOnly lastDayOfMonth = new(year, month, DateTime.DaysInMonth(year, month));

        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        List<Goal> goalsOverlappingMonth = await dbContext.Goals
            .AsNoTracking()
            .Where(goal =>
                goal.OwnerId == userId &&
                goal.Fin >= firstDayOfMonth &&
                goal.Debut <= lastDayOfMonth)
            .ToListAsync();

        var coverageMap = new Dictionary<DateOnly, bool>();

        for (int day = 1; day <= lastDayOfMonth.Day; day++)
        {
            var date = new DateOnly(year, month, day);

            List<Goal> coveringGoals = goalsOverlappingMonth
                .Where(goal => goal.Debut <= date && goal.Fin >= date)
                .ToList();

            if (coveringGoals.Count == 0)
            {
                continue;
            }

            bool allDone = coveringGoals.All(goal => goal.IsDone);
            coverageMap[date] = allDone;
        }

        return coverageMap;
    }

    private static void ValidateGoalDates(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("Fin doit être >= Début");
        }
    }
}
