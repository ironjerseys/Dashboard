using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Dashboard.Business;

// Décide si le titre d'une offre correspond à un poste.
// - Sans tenir compte des majuscules, des accents ni de la ponctuation : "Analyste d’Affaires"
//   et "analyste d'affaires" sont identiques.
// - Mots entiers : "ba" trouve "Senior BA" mais pas "MBA" ; ".net" ne trouve pas ".network".
// - Le pluriel en "s" est accepté : "business analyst" trouve "Business Analysts".
// - Le titre doit contenir au moins un terme inclus et aucun terme exclu. Sans terme inclus,
//   tout titre correspond (poste non filtré).
public sealed class JobTitleMatcher
{
    private readonly Regex? _include;
    private readonly Regex? _exclude;

    public JobTitleMatcher(string? includeTerms, string? excludeTerms)
    {
        _include = BuildRegex(includeTerms);
        _exclude = BuildRegex(excludeTerms);
    }

    public bool IsFiltered => _include is not null;

    public bool Matches(string? title)
    {
        var t = Normalize(title);
        if (_include is not null && !_include.IsMatch(t)) return false;
        if (_exclude is not null && _exclude.IsMatch(t)) return false;
        return true;
    }

    // Un terme par ligne (les ";" sont aussi acceptés).
    public static List<string> SplitTerms(string? terms) =>
        (terms ?? "")
            .Split(['\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => Normalize(t).Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    // Minuscules, sans accents ; tout sauf lettres, chiffres, # + . devient un espace.
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) || c is '#' or '+' or '.' ? c : ' ');
        }
        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    private static Regex? BuildRegex(string? terms)
    {
        var parts = SplitTerms(terms).Select(term =>
        {
            var n = Normalize(term);
            var pattern = Regex.Escape(n).Replace(@"\ ", " ");
            // Bord de mot seulement du côté où le terme commence / finit par une lettre ou un chiffre.
            if (char.IsLetterOrDigit(n[0])) pattern = @"(?<![\p{L}\p{N}])" + pattern;
            if (char.IsLetterOrDigit(n[^1])) pattern += @"s?(?![\p{L}\p{N}])";
            return pattern;
        }).ToList();

        return parts.Count == 0
            ? null
            : new Regex(string.Join("|", parts), RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }
}
