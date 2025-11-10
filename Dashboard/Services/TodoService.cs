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
}

public class TodoService : ITodoService
{
    private readonly BlogContext _db;
    public TodoService(BlogContext db) => _db = db;

    public Task<List<Todo>> GetAllAsync() => _db.Todos.OrderBy(t => t.Id).ToListAsync();
    public Task<Todo?> GetAsync(int id) => _db.Todos.FirstOrDefaultAsync(t => t.Id == id);

    public async Task<int> CreateAsync(string description)
    {
        var todo = new Todo { Description = description };
        _db.Todos.Add(todo);
        await _db.SaveChangesAsync();
        return todo.Id;
    }

    public async Task<bool> UpdateAsync(int id, string description)
    {
        var todo = await _db.Todos.FirstOrDefaultAsync(t => t.Id == id);
        if (todo == null) return false;
        todo.Description = description;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var todo = await _db.Todos.FirstOrDefaultAsync(t => t.Id == id);
        if (todo == null) return false;
        _db.Todos.Remove(todo);
        await _db.SaveChangesAsync();
        return true;
    }
}
