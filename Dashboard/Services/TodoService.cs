using Dashboard.Data;
using Dashboard.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Services;



public interface ITodoService
{
    Task<List<Todo>> GetAllAsync();
    Task<Todo?> GetAsync(int id);

    Task<int> CreateAsync(string description);
    Task<bool> UpdateAsync(int id, string description);
    Task<bool> DeleteAsync(int id);
    Task<bool> ToggleDoneAsync(int id);

    Task<List<Todo>> GetOpenAsync();
    Task<List<Todo>> GetDoneInPeriodAsync(DateTime start, DateTime end);
}

public class TodoService : ITodoService
{
    private readonly IDbContextFactory<BlogContext> _dbContextFactory;

    public TodoService(IDbContextFactory<BlogContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<Todo>> GetAllAsync()
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.Todos
            .AsNoTracking()
            .OrderBy(todoItem => todoItem.IsDone)
            .ThenBy(todoItem => todoItem.Id)
            .ToListAsync();
    }

    public async Task<Todo?> GetAsync(int id)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.Todos
            .AsNoTracking()
            .FirstOrDefaultAsync(todoItem => todoItem.Id == id);
    }

    public async Task<int> CreateAsync(string description)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        var todoItem = new Todo
        {
            Description = description.Trim(),
            IsDone = false,
            DoneAt = null
        };

        dbContext.Todos.Add(todoItem);
        await dbContext.SaveChangesAsync();

        return todoItem.Id;
    }

    public async Task<bool> UpdateAsync(int id, string description)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        Todo? todoItem = await dbContext.Todos.FirstOrDefaultAsync(todo => todo.Id == id);
        if (todoItem is null)
        {
            return false;
        }

        todoItem.Description = description.Trim();
        await dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        Todo? todoItem = await dbContext.Todos.FirstOrDefaultAsync(todo => todo.Id == id);
        if (todoItem is null)
        {
            return false;
        }

        dbContext.Todos.Remove(todoItem);
        await dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ToggleDoneAsync(int id)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        Todo? todoItem = await dbContext.Todos.FirstOrDefaultAsync(todo => todo.Id == id);
        if (todoItem is null)
        {
            return false;
        }

        todoItem.IsDone = !todoItem.IsDone;
        todoItem.DoneAt = todoItem.IsDone ? DateTime.UtcNow : null;

        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<Todo>> GetOpenAsync()
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.Todos
            .AsNoTracking()
            .Where(todoItem => !todoItem.IsDone)
            .OrderBy(todoItem => todoItem.Id)
            .ToListAsync();
    }

    public async Task<List<Todo>> GetDoneInPeriodAsync(DateTime start, DateTime end)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.Todos
            .AsNoTracking()
            .Where(todoItem =>
                todoItem.IsDone &&
                todoItem.DoneAt.HasValue &&
                todoItem.DoneAt.Value >= start &&
                todoItem.DoneAt.Value <= end)
            .OrderByDescending(todoItem => todoItem.DoneAt)
            .ToListAsync();
    }
}
