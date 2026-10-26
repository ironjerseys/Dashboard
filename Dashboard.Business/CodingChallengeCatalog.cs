namespace Dashboard.Business;

public sealed record CodingChallengeInfo(string Key, string Title, string Description, string Href, string Icon);

// Liste des défis de code (codés en dur, une page par défi).
// La clé doit correspondre au ChallengeKey utilisé par la page du défi.
public static class CodingChallengeCatalog
{
    public static readonly IReadOnlyList<CodingChallengeInfo> All = new CodingChallengeInfo[]
    {
        new("singleton", "Singleton Pattern",
            "Implement a thread-safe singleton with a private constructor and a single shared instance.",
            "/coding/singleton", "bi-box"),
        new("two-sum", "Two Sum",
            "Return the indices of the two numbers that add up to a target. A classic interview warm-up.",
            "/coding/two-sum", "bi-plus-slash-minus"),
        new("three-sum", "Three Sum",
            "Find all unique triplets that sum to zero. A very common interview question.",
            "/coding/three-sum", "bi-collection"),
    };

    public static string SqlKey(int sqlChallengeId) => $"sql-{sqlChallengeId}";

    // "sql-12" -> 12 ; null pour une clé de défi de code.
    public static int? ParseSqlId(string challengeKey) =>
        challengeKey.StartsWith("sql-", StringComparison.Ordinal)
        && int.TryParse(challengeKey.AsSpan(4), out int id)
            ? id
            : null;
}
