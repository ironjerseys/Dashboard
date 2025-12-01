namespace Dashboard.Services;

using Dashboard.Entities;
using Data;
using Microsoft.EntityFrameworkCore;
using Models;
using Ganss.Xss; // HtmlSanitizer

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
    private readonly HtmlSanitizer _sanitizer;

    public ArticleService(BlogContext db)
    {
        _db = db;
        _sanitizer = new HtmlSanitizer();
        // Ajouter les tags nécessaires (AllowedTags est read-only, on enrichit)
        foreach(var t in new [] { "code", "pre", "span", "table", "thead", "tbody", "tr", "th", "td", "h2", "h3", "h4", "img" })
            _sanitizer.AllowedTags.Add(t);
        _sanitizer.AllowDataAttributes = false;
        foreach(var a in new [] { "style", "src", "alt", "title", "width", "height" })
            _sanitizer.AllowedAttributes.Add(a);
        // Autoriser les URI data: pour les images collées en base64
        _sanitizer.AllowedSchemes.Add("data");
    }

    public async Task<IEnumerable<Article>> GetArticles()
    {
        var r = await _db.Articles.Include(a => a.Author).OrderByDescending(a => a.DateCreation).ToListAsync();
        return r;
    }

    public async Task<Article> GetArticle(int id)
    {
        return await _db.Articles.FindAsync(id) ?? new Article();
    }

    public async Task CreateArticle(Article article)
    {
        article.Contenu = _sanitizer.Sanitize(article.Contenu);
        _db.Articles.Add(article);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateArticle(Article article)
    {
        article.Contenu = _sanitizer.Sanitize(article.Contenu);
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
