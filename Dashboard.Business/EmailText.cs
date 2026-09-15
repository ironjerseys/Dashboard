using System.Net;
using System.Text.RegularExpressions;
using Dashboard.Persistance.Entities;

namespace Dashboard.Business;

/// <summary>Texte lisible d'un mail, pour l'analyse et l'affichage.</summary>
public static partial class EmailText
{
    /// <summary>Corps texte si present, sinon le HTML reduit a son texte.</summary>
    public static string BodyOf(EmailMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.TextBody))
        {
            return message.TextBody.Trim();
        }

        return string.IsNullOrWhiteSpace(message.HtmlBody) ? "" : HtmlToText(message.HtmlBody);
    }

    public static string HtmlToText(string html)
    {
        string text = HiddenBlocks().Replace(html, " ");
        text = LineBreakTags().Replace(text, "\n");
        text = AnyTag().Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        text = SpacesAndTabs().Replace(text, " ");
        text = BlankLines().Replace(text, "\n\n");
        return text.Trim();
    }

    [GeneratedRegex(@"<(style|script|head)\b[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HiddenBlocks();

    [GeneratedRegex(@"<(br|/p|/div|/tr|/li|/h[1-6])\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakTags();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex SpacesAndTabs();

    [GeneratedRegex(@"\s*\n\s*\n\s*")]
    private static partial Regex BlankLines();
}
