using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboard.Persistance.Entities;

public class AIChessLogs
{
    [Key]
    public int Id { get; set; } // identity (migration will set auto increment)

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    // "information" | "error" (ou autre) — libre selon vos usages
    [MaxLength(32)]
    public string Type { get; set; } = "information";

    // Paramètres/mesures d’analyse
    public int SearchDepth { get; set; }                 // profondeur demandée
    public long DurationMs { get; set; }                 // durée de réflexion
    public int LegalMovesCount { get; set; }             // coups listés (légaux)
    public int EvaluatedMovesCount { get; set; }         // coups effectivement évalués

    // Résultat
    [MaxLength(16)] public string? BestMoveUci { get; set; } // ex: e2e4
    public int? BestScoreCp { get; set; }                    // score en centi-pions


    public long GeneratedMovesTotal { get; set; }      // total coups générés (arbre)
    public long NodesVisited { get; set; }             // nœuds visités
    public long LeafEvaluations { get; set; }          // feuilles évaluées


    // Détails des évaluations par coup (JSON: [{ "moveUci":"e2e4", "scoreCp":34 }, ...])
    [Column(TypeName = "nvarchar(max)")]
    public string? EvaluatedMovesJson { get; set; }
}
