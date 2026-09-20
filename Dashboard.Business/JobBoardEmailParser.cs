using System.Text.RegularExpressions;
using Dashboard.Persistance.Entities;

namespace Dashboard.Business;

public sealed record JobBoardFacts(string Company, string? Location);

/// <summary>
/// Lit l'entreprise dans les confirmations des sites d'emploi (Indeed) : l'expediteur est le site,
/// l'entreprise n'apparait que dans le corps. Gratuit et deterministe, il corrige l'analyse de Claude
/// et repare les mails deja analyses.
/// </summary>
public static partial class JobBoardEmailParser
{
    public static bool IsIndeed(string fromAddress) =>
        fromAddress.EndsWith("@indeed.com", StringComparison.OrdinalIgnoreCase)
        || fromAddress.EndsWith(".indeed.com", StringComparison.OrdinalIgnoreCase);

    /// <summary>Null si le mail ne vient pas d'Indeed ou ne suit pas le modele de confirmation.</summary>
    public static JobBoardFacts? TryParse(EmailMessage email)
    {
        if (!IsIndeed(email.FromAddress))
        {
            return null;
        }

        // Le texte brut d'Indeed est parfois plus pauvre que le HTML : on essaie les deux.
        IEnumerable<string> bodies = new[]
            {
                email.TextBody,
                string.IsNullOrWhiteSpace(email.HtmlBody) ? null : EmailText.HtmlToText(email.HtmlBody)
            }
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Cast<string>();

        // La partie texte donne souvent l'entreprise sans la ligne "Entreprise - Ville" : on continue
        // jusqu'au corps qui porte aussi le lieu, en gardant le premier resultat comme repli.
        JobBoardFacts? fallback = null;

        foreach (string body in bodies)
        {
            if (TryParseIndeed(body) is not { } facts)
            {
                continue;
            }

            if (facts.Location is not null)
            {
                return facts;
            }

            fallback ??= facts;
        }

        return fallback;
    }

    /// <summary>
    /// Complete la classification de Claude avec ce que le modele Indeed dit a coup sur. "Sent to X" ne
    /// figure que dans une confirmation de candidature.
    /// </summary>
    public static EmailClassification Refine(EmailMessage email, EmailClassification classification)
    {
        if (TryParse(email) is not { } facts)
        {
            return classification;
        }

        return classification with
        {
            EventType = classification.EventType == EmailEventType.NotApplication
                ? EmailEventType.ApplicationReceived
                : classification.EventType,
            Company = facts.Company,
            Location = facts.Location ?? classification.Location
        };
    }

    public static JobBoardFacts? TryParseIndeed(string body)
    {
        Match sentTo = SentTo().Match(body);

        if (!sentTo.Success)
        {
            return null;
        }

        string company = sentTo.Groups["company"].Value.Trim();

        if (company.Length == 0)
        {
            return null;
        }

        return new JobBoardFacts(company, FindLocation(body, company));
    }

    /// <summary>Sous le titre du poste : "Club Assurance - Longueuil, QC J4H 4A6", parfois coupe apres le tiret.</summary>
    private static string? FindLocation(string body, string company)
    {
        var header = new Regex(@"^[ \t]*" + Regex.Escape(company) + @"[ \t]*[-–—][ \t]*(?<rest>[^\n]*)$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        Match match = header.Match(body);

        if (!match.Success)
        {
            return null;
        }

        string location = match.Groups["rest"].Value.Trim();

        if (location.Length == 0)
        {
            location = body[(match.Index + match.Length)..]
                .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? "";
        }

        return location.Length == 0 || Reviews().IsMatch(location) ? null : location;
    }

    [GeneratedRegex(@"(?:items were sent to|ont été envoyés à|ont ete envoyes a)\s+(?<company>[^\n]+?)\.?\s*(?:Good luck|Bonne chance|$)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex SentTo();

    [GeneratedRegex(@"^\d[\d\s,.]*\s*(reviews?|avis)$", RegexOptions.IgnoreCase)]
    private static partial Regex Reviews();
}
