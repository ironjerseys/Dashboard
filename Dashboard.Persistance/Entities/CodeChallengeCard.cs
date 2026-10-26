using System.ComponentModel.DataAnnotations;

namespace Dashboard.Persistance.Entities;

// Carte de répétition espacée (Leitner) pour un défi de code, par utilisateur.
// Même fonctionnement que LeitnerCard pour les questions techniques, mais
// rattachée à un défi identifié par une clé (ex : "singleton").
public class CodeChallengeCard
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string OwnerId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ChallengeKey { get; set; } = string.Empty;

    // Renseigné uniquement pour un défi SQL (clé "sql-{id}") : clé étrangère vers
    // SqlChallenges avec suppression en cascade. Null pour un défi de code.
    public int? SqlChallengeId { get; set; }

    [Range(1, 20)]
    public int Box { get; set; } = 1;

    // Date (UTC) à laquelle le défi doit réapparaître
    public DateOnly NextDueDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public DateTime? LastReviewedUtc { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
