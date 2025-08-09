namespace Dashboard.Services;

using Data;
using Microsoft.EntityFrameworkCore;
using Models;

public interface IArticleService
{
    Task<IEnumerable<Article>> GetArticles();
    Task<Article> GetArticle(int id);
    Task CreateArticle(Article article);
    Task UpdateArticle(Article article);
    Task Delete(int id);
}

public class ArticleService : IArticleService
{

    private readonly BlogContext _db;

    public ArticleService(BlogContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Article>> GetArticles()
    {
        return await _db.Articles.Include(a => a.Author).OrderByDescending(a => a.DateCreation).ToListAsync();
    }

    public async Task<Article> GetArticle(int id)
    {
        return await _db.Articles.FindAsync(id);
    }

    // Creat article
    public async Task CreateArticle(Article article)
    {
        _db.Articles.Add(article);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateArticle(Article article)
    {
        _db.Articles.Update(article);
        await _db.SaveChangesAsync();
    }

    public async Task Delete(int id)
    {
        var article = await _db.Articles.FindAsync(id);
        if (article == null) return;
        _db.Articles.Remove(article);
        await _db.SaveChangesAsync();
    }
}
