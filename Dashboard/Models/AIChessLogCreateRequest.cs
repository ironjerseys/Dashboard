namespace Dashboard.Models;

public sealed class AIChessLogCreateRequest
{
    public string Type { get; set; } = "information";
    public int SearchDepth { get; set; }
    public long DurationMs { get; set; }
    public int LegalMovesCount { get; set; }
    public int EvaluatedMovesCount { get; set; }

    public string? BestMoveUci { get; set; }
    public int? BestScoreCp { get; set; }

    public long GeneratedMovesTotal { get; set; }
    public long NodesVisited { get; set; }
    public long LeafEvaluations { get; set; }

    public string? EvaluatedMovesJson { get; set; }
    public int? ArticleId { get; set; }
}
