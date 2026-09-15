using System.Globalization;
using System.Text;
using Dashboard.Persistance.Entities;

namespace Dashboard.Business;

/// <summary>Candidature existante, reduite a ce qui sert au rattachement.</summary>
public sealed record ApplicationCandidate(int Id, string Company, string? Position, JobApplicationStatus Status, DateTime LastEventUtc);

public enum MatchDecision
{
    /// <summary>Rattacher a <see cref="MatchResult.ApplicationId"/>.</summary>
    Link,

    /// <summary>Creer une nouvelle candidature a partir du mail.</summary>
    Create,

    /// <summary>Plusieurs candidatures possibles, ou pas assez d'information : a trancher a la main.</summary>
    Review,

    /// <summary>Rien a rattacher (mail sans rapport, ou mise a jour sans candidature connue).</summary>
    Ignore
}

public sealed record MatchResult(MatchDecision Decision, int? ApplicationId = null);

/// <summary>
/// Decide a quelle candidature appartient un mail analyse. Volontairement en code et non
/// confie a Claude : c'est gratuit, deterministe, et on sait expliquer chaque decision.
/// </summary>
public static class JobApplicationMatcher
{
    private const double SamePositionThreshold = 0.6;
    private const double LikelyPositionThreshold = 0.5;

    private static readonly HashSet<string> CompanyNoise = new(StringComparer.Ordinal)
    {
        "inc", "incorporated", "ltd", "limited", "llc", "llp", "corp", "corporation", "co", "company",
        "plc", "sa", "sas", "gmbh", "ag", "group", "the", "canada", "careers", "jobs", "talent", "acquisition"
    };

    private static readonly HashSet<string> AcronymSkip = new(StringComparer.Ordinal) { "of", "and", "de", "des", "du", "la", "le" };

    private static readonly Dictionary<string, string> PositionSynonyms = new(StringComparer.Ordinal)
    {
        ["sr"] = "senior",
        ["jr"] = "junior",
        ["dev"] = "developer",
        ["developpeur"] = "developer",
        ["eng"] = "engineer",
        ["engineering"] = "engineer",
        ["ingenieur"] = "engineer",
        ["mgr"] = "manager",
        ["net"] = "dotnet"
    };

    /// <param name="sentUtc">Date du mail : une confirmation posterieure a une candidature close est une nouvelle candidature.</param>
    public static MatchResult Match(EmailClassification email, DateTime sentUtc, IReadOnlyList<ApplicationCandidate> applications)
    {
        if (email.EventType == EmailEventType.NotApplication)
        {
            return new MatchResult(MatchDecision.Ignore);
        }

        if (email.Company is null)
        {
            // Une reponse dont on ne sait pas de quelle entreprise elle vient ne peut pas etre devinee.
            return new MatchResult(email.EventType == EmailEventType.OtherUpdate ? MatchDecision.Ignore : MatchDecision.Review);
        }

        List<ApplicationCandidate> sameCompany = applications
            .Where(a => SameCompany(a.Company, email.Company))
            .ToList();

        if (email.EventType == EmailEventType.ApplicationReceived)
        {
            // Deux confirmations pour la meme offre (LinkedIn puis la plateforme) : une seule candidature.
            // Mais repostuler apres un refus en ouvre une nouvelle.
            ApplicationCandidate? duplicate = sameCompany
                .Where(a => IsOpen(a.Status) || a.LastEventUtc >= sentUtc)
                .Where(a => PositionSimilarity(a.Position, email.Position) >= SamePositionThreshold)
                .OrderByDescending(a => a.LastEventUtc)
                .FirstOrDefault();

            return duplicate is null
                ? new MatchResult(MatchDecision.Create)
                : new MatchResult(MatchDecision.Link, duplicate.Id);
        }

        if (sameCompany.Count == 0)
        {
            // Reponse sans confirmation connue (mail de confirmation jamais recu ou hors du libelle).
            return new MatchResult(email.EventType == EmailEventType.OtherUpdate ? MatchDecision.Ignore : MatchDecision.Create);
        }

        if (sameCompany.Count == 1)
        {
            return new MatchResult(MatchDecision.Link, sameCompany[0].Id);
        }

        if (email.Position is not null)
        {
            var scored = sameCompany
                .Select(a => (Application: a, Score: PositionSimilarity(a.Position, email.Position)))
                .OrderByDescending(x => x.Score)
                .ToList();

            bool clearWinner = scored[0].Score >= LikelyPositionThreshold
                               && (scored.Count == 1 || scored[0].Score > scored[1].Score);

            if (clearWinner)
            {
                return new MatchResult(MatchDecision.Link, scored[0].Application.Id);
            }
        }

        // Sans poste exploitable, une seule candidature encore ouverte chez cette entreprise suffit.
        List<ApplicationCandidate> open = sameCompany.Where(a => IsOpen(a.Status)).ToList();

        if (open.Count == 1)
        {
            return new MatchResult(MatchDecision.Link, open[0].Id);
        }

        return new MatchResult(email.EventType == EmailEventType.OtherUpdate ? MatchDecision.Ignore : MatchDecision.Review);
    }

    /// <summary>
    /// Repercute un mail rattache sur sa candidature. Les mails sont analyses du plus ancien au plus
    /// recent, mais un mail en retard ne doit pas ecraser un statut plus recent.
    /// </summary>
    public static void ApplyEmail(JobApplication application, EmailMessage email)
    {
        application.Position ??= email.ExtractedPosition;
        application.Location ??= email.ExtractedLocation;

        if (email.EventType == EmailEventType.ApplicationReceived)
        {
            if (application.AppliedUtc is null || email.SentUtc < application.AppliedUtc)
            {
                application.AppliedUtc = email.SentUtc;
            }
        }
        else if (email.EventType is { } eventType
                 && StatusFor(eventType) is { } status
                 && email.SentUtc >= application.LastEventUtc)
        {
            application.Status = status;
        }

        if (email.SentUtc > application.LastEventUtc)
        {
            application.LastEventUtc = email.SentUtc;
        }
    }

    public static bool IsOpen(JobApplicationStatus status) =>
        status is not (JobApplicationStatus.Rejected or JobApplicationStatus.Offer);

    /// <summary>Statut que le mail donne a la candidature ; null s'il ne le change pas.</summary>
    public static JobApplicationStatus? StatusFor(EmailEventType eventType) => eventType switch
    {
        EmailEventType.ApplicationReceived => JobApplicationStatus.Applied,
        EmailEventType.Assessment => JobApplicationStatus.Assessment,
        EmailEventType.Interview => JobApplicationStatus.Interview,
        EmailEventType.Offer => JobApplicationStatus.Offer,
        EmailEventType.Rejection => JobApplicationStatus.Rejected,
        _ => null
    };

    public static bool SameCompany(string a, string b)
    {
        List<string> left = CompanyTokens(a);
        List<string> right = CompanyTokens(b);

        if (left.Count == 0 || right.Count == 0)
        {
            return false;
        }

        if (left.SequenceEqual(right))
        {
            return true;
        }

        // "TD" et "TD Bank", "Deloitte" et "Deloitte Digital" : l'un contient tous les mots de l'autre.
        if (left.All(right.Contains) || right.All(left.Contains))
        {
            return true;
        }

        // "RBC" et "Royal Bank of Canada".
        return IsAcronym(left, b) || IsAcronym(right, a);
    }

    private static bool IsAcronym(List<string> candidateTokens, string fullName)
    {
        if (candidateTokens.Count != 1)
        {
            return false;
        }

        string acronym = string.Concat(RawTokens(fullName)
            .Where(t => !AcronymSkip.Contains(t))
            .Select(t => t[0]));

        return acronym.Length >= 2 && acronym == candidateTokens[0];
    }

    /// <summary>Recouvrement des mots du poste (Jaccard), de 0 a 1. Deux postes inconnus comptent comme identiques.</summary>
    public static double PositionSimilarity(string? a, string? b)
    {
        if (a is null && b is null)
        {
            return 1;
        }

        if (a is null || b is null)
        {
            return 0;
        }

        HashSet<string> left = PositionTokens(a);
        HashSet<string> right = PositionTokens(b);

        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        int common = left.Count(right.Contains);
        return (double)common / (left.Count + right.Count - common);
    }

    private static List<string> CompanyTokens(string value) =>
        RawTokens(value).Where(t => !CompanyNoise.Contains(t)).ToList();

    private static HashSet<string> PositionTokens(string value) =>
        RawTokens(value)
            .Where(t => !t.All(char.IsDigit))
            .Select(t => PositionSynonyms.GetValueOrDefault(t, t))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Minuscules, sans accents, "&amp;" en "and", decoupe sur tout ce qui n'est pas lettre ou chiffre.</summary>
    private static List<string> RawTokens(string value)
    {
        string normalized = value.Replace("&", " and ").Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (char c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }

        return builder.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }
}
