using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Dashboard.Services.Helpers;

public static class SlugHelper
{
    public static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "article";

        // lower
        input = input.Trim().ToLowerInvariant();

        // remove accents
        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        var noAccents = sb.ToString().Normalize(NormalizationForm.FormC);

        // keep letters/numbers, replace others by '-'
        noAccents = Regex.Replace(noAccents, @"[^a-z0-9]+", "-");
        noAccents = Regex.Replace(noAccents, @"-+", "-").Trim('-');

        return string.IsNullOrWhiteSpace(noAccents) ? "article" : noAccents;
    }
}
