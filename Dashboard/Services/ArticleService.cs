using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
using Dashboard.Persistance.Entities.Enums;
using Dashboard.Services.Helpers;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Services;

public interface IArticleService
{
    Task<IEnumerable<Article>> GetArticlesAsync(IEnumerable<int>? includeLabelIds = null, ArticleSort sort = ArticleSort.DateNewest, string? search = null);
    Task<Article> GetArticleByIdAsync(int id);
    Task CreateArticleAsync(Article article, string[]? newLabels = null, int[]? selectedLabelIds = null);
    Task UpdateArticleAsync(Article article, string[]? newLabels = null, int[]? selectedLabelIds = null);
    Task DeleteAsync(int id);
    Task<List<Label>> GetLabelsAsync();
    Task<Article?> GetArticleBySlugAsync(string slug);
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

        var q = _context.Articles
            .Include(a => a.Author)
            .Include(a => a.Labels)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(a =>
                EF.Functions.Like(a.Titre, "%" + term + "%") ||
                EF.Functions.Like(a.Contenu, "%" + term + "%"));
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

    public async Task<Article> GetArticleByIdAsync(int id)
    {
        await using BlogContext _context = await _dbContextFactory.CreateDbContextAsync();

        return await _context.Articles
            .Include(a => a.Labels)
            .FirstOrDefaultAsync(a => a.Id == id) ?? new Article();
    }

    private async Task<List<Label>> EnsureLabelsAsync(BlogContext context, string[]? newLabels)
    {
        var result = new List<Label>();
        if (newLabels is null || newLabels.Length == 0) return result;

        var names = newLabels
            .SelectMany(raw => (raw ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (names.Count == 0) return result;

        var existing = await context.Labels
            .Where(l => names.Contains(l.Name))
            .ToListAsync();

        var existingByName = existing.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            if (existingByName.TryGetValue(name, out var found))
            {
                result.Add(found);
            }
            else
            {
                var created = new Label { Name = name };
                context.Labels.Add(created);
                result.Add(created);
            }
        }

        return result;
    }

    private async Task<string> EnsureUniqueSlugAsync(BlogContext ctx, string title, int? currentArticleId = null)
    {
        var baseSlug = SlugHelper.Slugify(title);
        var slug = baseSlug;
        var i = 2;

        while (true)
        {
            var exists = await ctx.Articles.AnyAsync(a => a.Slug == slug && a.Id != currentArticleId);

            if (!exists)
                return slug;

            slug = $"{baseSlug}-{i}";
            i++;
        }
    }


    public async Task<List<Label>> GetLabelsAsync()
    {
        await using BlogContext _context = await _dbContextFactory.CreateDbContextAsync();
        return await _context.Labels.OrderBy(l => l.Name).ToListAsync();
    }

    private static string SanitizeHtml(HtmlSanitizer sanitizer, string? html)
        => sanitizer.Sanitize(html ?? string.Empty);

    private static void DedupLabelsInto(Article article, List<Label> labels)
    {
        var dedup = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in labels)
        {
            var key = l.Id != 0 ? $"id:{l.Id}" : $"name:{l.Name}";
            if (!dedup.ContainsKey(key))
                dedup[key] = l;
        }
        article.Labels = dedup.Values.ToList();
    }

    private static async Task EnsureCoverExistsOrNullAsync(BlogContext context, Article article)
    {
        // Si tu n'as pas de CoverMediaId dans Article, supprime cette méthode.
        if (article.CoverMediaId is null) return;

        var exists = await context.MediaAssets.AsNoTracking()
            .AnyAsync(m => m.Id == article.CoverMediaId.Value);

        if (!exists)
        {
            // au choix : throw, ou remettre à null
            article.CoverMediaId = null;
        }
    }

    public async Task CreateArticleAsync(Article article, string[]? newLabels = null, int[]? selectedLabelIds = null)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        article.Contenu = SanitizeHtml(_sanitizer, article.Contenu);

        article.Slug = await EnsureUniqueSlugAsync(context, article.Titre);

        // (optionnel) valide CoverMediaId si tu as ajouté cette colonne
        await EnsureCoverExistsOrNullAsync(context, article);

        var labels = new List<Label>();

        if (selectedLabelIds is { Length: > 0 })
        {
            var existing = await context.Labels
                .Where(l => selectedLabelIds.Contains(l.Id))
                .ToListAsync();
            labels.AddRange(existing);
        }

        var ensured = await EnsureLabelsAsync(context, newLabels);
        labels.AddRange(ensured);

        DedupLabelsInto(article, labels);

        context.Articles.Add(article);
        await context.SaveChangesAsync();
    }

    public async Task UpdateArticleAsync(Article article, string[]? newLabels = null, int[]? selectedLabelIds = null)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var sanitized = SanitizeHtml(_sanitizer, article.Contenu);

        var existing = await context.Articles
            .Include(a => a.Labels)
            .FirstOrDefaultAsync(a => a.Id == article.Id);

        if (existing is null) return;

        // Scalars
        existing.Titre = article.Titre;
        existing.Contenu = sanitized;
        existing.IsPublic = article.IsPublic;

        existing.Slug = await EnsureUniqueSlugAsync(context, existing.Titre ?? "article", existing.Id);

        // IMPORTANT: image de couverture
        existing.CoverMediaId = article.CoverMediaId;

        // (optionnel) valide l'existence (si invalide -> null)
        await EnsureCoverExistsOrNullAsync(context, existing);

        // Labels
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

    public async Task<Article?> GetArticleBySlugAsync(string slug)
    {
        await using var ctx = await _dbContextFactory.CreateDbContextAsync();
        return await ctx.Articles
            .Include(a => a.Labels)
            .FirstOrDefaultAsync(a => a.Slug == slug);
    }
}
