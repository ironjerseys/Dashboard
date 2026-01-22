using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
using Dashboard.Persistance.Entities.Enums;

namespace Dashboard.Services;


public interface IArticleService
{
    Task<IEnumerable<Article>> GetArticlesAsync(IEnumerable<int>? includeLabelIds = null, ArticleSort sort = ArticleSort.DateNewest, string? search = null);
    Task<Article> GetArticleAsync(int id);
    Task CreateArticleAsync(Article article, string[]? newLabels = null, int[]? selectedLabelIds = null);
    Task UpdateArticleAsync(Article article, string[]? newLabels = null, int[]? selectedLabelIds = null);
    Task DeleteAsync(int id);
    Task<List<Label>> GetLabelsAsync();
}

public class ArticleService : IArticleService
{
    private readonly IDbContextFactory<BlogContext> _dbContextFactory;
    private readonly HtmlSanitizer _sanitizer;

    public ArticleService(IDbContextFactory<BlogContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
        _sanitizer = new HtmlSanitizer();
        foreach (var t in new[] { "code", "pre", "span", "table", "thead", "tbody", "tr", "th", "td", "h2", "h3", "h4", "img" })
            _sanitizer.AllowedTags.Add(t);
        _sanitizer.AllowDataAttributes = false;
        foreach (var a in new[] { "style", "src", "alt", "title", "width", "height" })
            _sanitizer.AllowedAttributes.Add(a);
    }

    public async Task<IEnumerable<Article>> GetArticlesAsync(IEnumerable<int>? includeLabelIds = null, ArticleSort sort = ArticleSort.DateNewest, string? search = null)
    {
        await using BlogContext _context = await _dbContextFactory.CreateDbContextAsync();

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

    public async Task<Article> GetArticleAsync(int id)
    {
        await using BlogContext _context = await _dbContextFactory.CreateDbContextAsync();

        return await _context.Articles.Include(a => a.Labels).FirstOrDefaultAsync(a => a.Id == id) ?? new Article();
    }

    private async Task<List<Label>> EnsureLabelsAsync(BlogContext context, string[]? newLabels)
    {
        var result = new List<Label>();
        if (newLabels is null || newLabels.Length == 0) return result;

        // Normalise: split CSV + trim + distinct
        var names = newLabels
            .SelectMany(raw => (raw ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (names.Count == 0) return result;

        // Charge ceux qui existent déjà
        // (si ta collation SQL est case-insensitive, l'égalité suffit généralement)
        var existing = await context.Labels
            .Where(l => names.Contains(l.Name))
            .ToListAsync();

        var existingByName = existing.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

        // Ajoute existants + crée manquants
        foreach (var name in names)
        {
            if (existingByName.TryGetValue(name, out var found))
            {
                result.Add(found);
            }
            else
            {
                var created = new Label { Name = name };
                context.Labels.Add(created);     // pas de SaveChanges ici
                result.Add(created);
            }
        }

        return result;
    }


    public async Task<List<Label>> GetLabelsAsync()
    {
        await using BlogContext _context = await _dbContextFactory.CreateDbContextAsync();

        return await _context.Labels.OrderBy(l => l.Name).ToListAsync();
    }

    public async Task CreateArticleAsync(Article article, string[]? newLabels = null, int[]? selectedLabelIds = null)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        article.Contenu = _sanitizer.Sanitize(article.Contenu ?? string.Empty);

        var labels = new List<Label>();

        // 1) Labels existants sélectionnés (même DbContext)
        if (selectedLabelIds is { Length: > 0 })
        {
            var existing = await context.Labels
                .Where(l => selectedLabelIds.Contains(l.Id))
                .ToListAsync();

            labels.AddRange(existing);
        }

        // 2) Labels à créer si besoin (même DbContext)
        var ensured = await EnsureLabelsAsync(context, newLabels);
        labels.AddRange(ensured);

        // 3) Dédup (par Id si connu, sinon par Name)
        var dedup = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in labels)
        {
            var key = l.Id != 0 ? $"id:{l.Id}" : $"name:{l.Name}";
            if (!dedup.ContainsKey(key))
                dedup[key] = l;
        }

        article.Labels = dedup.Values.ToList();

        context.Articles.Add(article);
        await context.SaveChangesAsync();
    }


    public async Task UpdateArticleAsync(Article article, string[]? newLabels = null, int[]? selectedLabelIds = null)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var sanitized = _sanitizer.Sanitize(article.Contenu ?? string.Empty);

        // Charge l’entité suivie + labels
        var existing = await context.Articles
            .Include(a => a.Labels)
            .FirstOrDefaultAsync(a => a.Id == article.Id);

        if (existing is null) return;

        // Scalars
        existing.Titre = article.Titre;
        existing.Contenu = sanitized;
        existing.IsPublic = article.IsPublic;

        // Labels désirés (même DbContext)
        var desired = new List<Label>();

        if (selectedLabelIds is { Length: > 0 })
        {
            var existingLabels = await context.Labels
                .Where(l => selectedLabelIds.Contains(l.Id))
                .ToListAsync();

            desired.AddRange(existingLabels);
        }

        var ensured = await EnsureLabelsAsync(context, newLabels);
        desired.AddRange(ensured);

        // Dédup par Id si possible, sinon par Name
        var desiredById = desired
            .Where(l => l.Id != 0)
            .GroupBy(l => l.Id)
            .Select(g => g.First())
            .ToList();

        var desiredNamesForNew = desired
            .Where(l => l.Id == 0 && !string.IsNullOrWhiteSpace(l.Name))
            .GroupBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        desired = desiredById.Concat(desiredNamesForNew).ToList();

        // Remplacement simple : on met la collection exactement à l’état désiré
        existing.Labels.Clear();
        foreach (var l in desired)
            existing.Labels.Add(l);

        await context.SaveChangesAsync();
    }


    public async Task DeleteAsync(int id)
    {
        await using BlogContext _context = await _dbContextFactory.CreateDbContextAsync();

        var article = await _context.Articles.FindAsync(id);

        if (article == null) return;

        _context.Articles.Remove(article);
        await _context.SaveChangesAsync();
    }
}
