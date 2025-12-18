namespace Dashboard.Services;

using Dashboard.Entities;
using Data;
using Microsoft.EntityFrameworkCore;
using Models;
using Ganss.Xss; // HtmlSanitizer

public enum ArticleSort
{
    TitleAsc,
    TitleDesc,
    DateNewest,
    DateOldest
}

public interface IArticleService
{
    Task<IEnumerable<Article>> GetArticles(IEnumerable<int>? includeLabelIds = null, ArticleSort sort = ArticleSort.DateNewest, string? search = null);
    Task<Article> GetArticle(int id);
    Task CreateArticle(Article article, string[]? newLabels = null, int[]? selectedLabelIds = null);
    Task UpdateArticle(Article article, string[]? newLabels = null, int[]? selectedLabelIds = null);
    Task Delete(int id);
    Task<List<Label>> GetLabels();
}

public class ArticleService : IArticleService
{
    private readonly BlogContext _context;
    private readonly HtmlSanitizer _sanitizer;

    public ArticleService(BlogContext context)
    {
        _context = context;
        _sanitizer = new HtmlSanitizer();
        foreach(var t in new [] { "code", "pre", "span", "table", "thead", "tbody", "tr", "th", "td", "h2", "h3", "h4", "img" })
            _sanitizer.AllowedTags.Add(t);
        _sanitizer.AllowDataAttributes = false;
        foreach(var a in new [] { "style", "src", "alt", "title", "width", "height" })
            _sanitizer.AllowedAttributes.Add(a);
        _sanitizer.AllowedSchemes.Add("data");
    }

    public async Task<IEnumerable<Article>> GetArticles(IEnumerable<int>? includeLabelIds = null, ArticleSort sort = ArticleSort.DateNewest, string? search = null)
    {
        var q = _context.Articles.Include(a => a.Author).Include(a => a.Labels).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(a => EF.Functions.Like(a.Titre, "%" + term + "%") || EF.Functions.Like(a.Contenu, "%" + term + "%"));
        }
        if (includeLabelIds != null)
        {
            var ids = includeLabelIds.Where(id => id > 0).Distinct().ToArray();
            if (ids.Length > 0)
                q = q.Where(a => a.Labels.Any(l => ids.Contains(l.Id)));
        }
        q = sort switch
        {
            ArticleSort.TitleAsc => q.OrderBy(a => a.Titre),
            ArticleSort.TitleDesc => q.OrderByDescending(a => a.Titre),
            ArticleSort.DateOldest => q.OrderBy(a => a.DateCreation),
            _ => q.OrderByDescending(a => a.DateCreation)
        };
        return await q.ToListAsync();
    }

    public async Task<Article> GetArticle(int id)
    {
        return await _context.Articles.Include(a => a.Labels).FirstOrDefaultAsync(a => a.Id == id) ?? new Article();
    }

    private async Task<List<Label>> EnsureLabels(string[]? newLabels)
    {
        var result = new List<Label>();
        if (newLabels == null || newLabels.Length == 0) return result;
        foreach (var raw in newLabels)
        {
            var name = (raw ?? "").Trim();
            if (string.IsNullOrEmpty(name)) continue;
            var parts = name.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach(var p in parts)
            {
                var existing = await _context.Labels.FirstOrDefaultAsync(l => l.Name == p);
                if (existing != null) result.Add(existing);
                else {
                    var l = new Label { Name = p };
                    _context.Labels.Add(l);
                    await _context.SaveChangesAsync();
                    result.Add(l);
                }
            }
        }
        return result;
    }

    public async Task<List<Label>> GetLabels() => await _context.Labels.OrderBy(l => l.Name).ToListAsync();

    public async Task CreateArticle(Article article, string[]? newLabels = null, int[]? selectedLabelIds = null)
    {
        article.Contenu = _sanitizer.Sanitize(article.Contenu);
        var labels = new List<Label>();
        if (selectedLabelIds != null && selectedLabelIds.Length > 0)
        {
            var existing = await _context.Labels.Where(l => selectedLabelIds.Contains(l.Id)).ToListAsync();
            labels.AddRange(existing);
        }
        var created = await EnsureLabels(newLabels);
        labels.AddRange(created);
        article.Labels = labels.DistinctBy(l => l.Id).ToList();
        _context.Articles.Add(article);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateArticle(Article article, string[]? newLabels = null, int[]? selectedLabelIds = null)
    {
        var sanitized = _sanitizer.Sanitize(article.Contenu);

        // Load tracked entity including current labels
        var existing = await _context.Articles.Include(a => a.Labels).FirstOrDefaultAsync(a => a.Id == article.Id);
        if (existing == null) return;

        // Update scalar properties
        existing.Titre = article.Titre;
        existing.Contenu = sanitized;
        existing.IsPublic = article.IsPublic;
        // existing.DateCreation stays unchanged

        // Build desired labels set
        var desired = new List<Label>();
        if (selectedLabelIds != null && selectedLabelIds.Length > 0)
        {
            var existingLabels = await _context.Labels.Where(l => selectedLabelIds.Contains(l.Id)).ToListAsync();
            desired.AddRange(existingLabels);
        }
        var created = await EnsureLabels(newLabels);
        desired.AddRange(created);
        var desiredIds = desired.Select(l => l.Id).Distinct().ToHashSet();

        // Remove unselected labels
        var toRemove = existing.Labels.Where(l => !desiredIds.Contains(l.Id)).ToList();
        foreach (var l in toRemove) existing.Labels.Remove(l);

        // Add missing labels
        var existingIds = existing.Labels.Select(l => l.Id).ToHashSet();
        foreach (var l in desired)
        {
            if (!existingIds.Contains(l.Id))
            {
                // Ensure label is attached
                if (_context.Entry(l).State == EntityState.Detached)
                    _context.Labels.Attach(l);
                existing.Labels.Add(l);
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task Delete(int id)
    {
        var article = await _context.Articles.FindAsync(id);
        if (article == null) return;
        _context.Articles.Remove(article);
        await _context.SaveChangesAsync();
    }
}
