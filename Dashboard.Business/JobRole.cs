using System.Globalization;
using System.Text;

namespace Dashboard.Business;

/// <summary>
/// Range un intitule de poste en technique (developpeur, .NET, engineer, ingenieur) ou non technique
/// (business analyst, product owner, analyste fonctionnel, et tout le reste), d'apres des mots-cles.
/// </summary>
public static class JobRole
{
    /// <summary>Gagnent sur les mots techniques : "Software Product Owner" reste un poste non technique.</summary>
    private static readonly string[] NonTechnicalPhrases =
    [
        "business analyst", "analyste d affaires", "analyste affaires",
        "product owner", "proprietaire de produit",
        "analyste fonctionnel", "analyste fonctionnelle", "functional analyst"
    ];

    private static readonly HashSet<string> TechnicalWords = new(StringComparer.Ordinal)
    {
        "dev", "devs", "developpeur", "developpeuse", "developpeurs", "developer", "developers",
        "engineer", "engineers", "ingenieur", "ingenieure", "ingenieurs",
        "programmeur", "programmeuse", "programmer",
        "net", "dotnet", "csharp", "fullstack", "backend", "frontend", "devops", "sre",
        "software", "logiciel", "logiciels"
    };

    private static readonly string[] TechnicalPhrases = ["full stack", "back end", "front end"];

    /// <summary>Sans intitule, le poste compte comme non technique.</summary>
    public static bool IsTechnical(string? position)
    {
        if (string.IsNullOrWhiteSpace(position))
        {
            return false;
        }

        // Espaces autour pour ne trouver que des mots entiers.
        string text = $" {Normalize(position)} ";

        if (NonTechnicalPhrases.Any(p => text.Contains($" {p} ", StringComparison.Ordinal)))
        {
            return false;
        }

        return TechnicalPhrases.Any(p => text.Contains($" {p} ", StringComparison.Ordinal))
               || text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(TechnicalWords.Contains);
    }

    /// <summary>Minuscules, sans accents, ponctuation remplacee par des espaces (".NET" -> " net").</summary>
    private static string Normalize(string value)
    {
        string decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }

        // Espaces multiples ramenes a un seul pour que les expressions de deux mots se retrouvent.
        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
