using System.Globalization;
using System.Text;
using Dashboard.Persistance.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Business;

public enum MechanicCategory { Automobile, HeavyTruck, HeavyEquipment, Industrial, SmallEngine, Other }

public record MechanicJobItem(
    int Id,
    string Source,
    string Url,
    string Title,
    string Company,
    string? City,
    DateTime? DatePosted,
    DateTime? ExpiresAt,
    string? Salary,
    DateTime FirstSeenAt,
    MechanicCategory Category,
    bool IsApprentice,
    bool IsActive)
{
    /// Date de publication si la source la donne (Quebec emploi ne la donne pas), sinon premiere detection.
    public DateTime SortDate => DatePosted ?? FirstSeenAt;
}

public record MechanicJobList(DateTime? LastScrapeAt, List<MechanicJobItem> Jobs);

public interface IMechanicJobService
{
    Task<MechanicJobList> GetAllAsync(CancellationToken ct = default);
}

public sealed class MechanicJobService : IMechanicJobService
{
    private readonly IDbContextFactory<BlogContext> _factory;

    public MechanicJobService(IDbContextFactory<BlogContext> factory)
    {
        _factory = factory;
    }

    public async Task<MechanicJobList> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var rows = await db.MechanicJobPostings.AsNoTracking().ToListAsync(ct);
        if (rows.Count == 0) return new MechanicJobList(null, []);

        // Toutes les offres d'un run partagent le meme LastSeenAt : "active" = vue au dernier run et pas expiree.
        var lastRun = rows.Max(r => r.LastSeenAt);
        var today = DateTime.UtcNow.Date;

        var jobs = rows.Select(r => new MechanicJobItem(
                r.Id, r.Source, r.Url, r.Title,
                string.IsNullOrWhiteSpace(r.Company) ? "?" : r.Company.Trim(),
                r.City, r.DatePosted, r.ExpiresAt, r.Salary, r.FirstSeenAt,
                Classify(r.Title),
                IsApprenticeTitle(r.Title),
                r.LastSeenAt >= lastRun.AddHours(-1) && (r.ExpiresAt is null || r.ExpiresAt >= today)))
            .ToList();

        return new MechanicJobList(lastRun, jobs);
    }

    // Ordre important : "mecanicien de chantier d'equipement lourd" est de l'equipement lourd, pas de l'industriel.
    private static readonly (MechanicCategory Category, string[] Keywords)[] CategoryKeywords =
    [
        (MechanicCategory.SmallEngine, ["petits moteurs", "petit moteur", "small engine", "moto", "vtt", "motoneige", "marine", "nautique", "recreati", "velo"]),
        (MechanicCategory.HeavyTruck, ["camion", "poids lourd", "truck", "remorque", "trailer", "autobus", "diesel", "transport"]),
        (MechanicCategory.HeavyEquipment, ["equipement lourd", "machinerie", "heavy", "agricole", "agriculture", "farm", "construction", "foresti"]),
        (MechanicCategory.Industrial, ["industriel", "industrial", "chantier", "millwright"]),
        (MechanicCategory.Automobile, ["auto", "voiture", "vehicule", "garage", "automotive", "car ", "pneu", "toyota", "honda", "ford", "mazda", "subaru", "hyundai", "kia", "nissan", "chevrolet", "gm ", "chrysler"]),
    ];

    public static MechanicCategory Classify(string title)
    {
        var t = Normalize(title) + " ";
        foreach (var (category, keywords) in CategoryKeywords)
        {
            if (keywords.Any(t.Contains)) return category;
        }
        return MechanicCategory.Other;
    }

    public static bool IsApprenticeTitle(string title)
    {
        var t = Normalize(title);
        return t.Contains("apprenti") || t.Contains("apprentice") || t.Contains("aide-mecan")
            || t.Contains("aide mecan") || t.Contains("helper") || t.Contains("stagiaire");
    }

    /// Minuscules sans accents, pour comparer "Mécanicien" et "mecanicien".
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        return sb.ToString();
    }
}
