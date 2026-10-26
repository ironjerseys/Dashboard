using System.ComponentModel.DataAnnotations;

namespace Dashboard.Persistance.Entities;

// Défi (code ou SQL) désactivé par un administrateur, identifié par sa clé
// ("singleton", "two-sum", "sql-12"...). Absent de la table = actif.
// Un défi désactivé n'apparaît plus dans les listes ni dans les compteurs "dus".
public class InactiveChallenge
{
    [Key, MaxLength(100)]
    public string ChallengeKey { get; set; } = string.Empty;

    // Renseigné uniquement pour un défi SQL (clé "sql-{id}") : clé étrangère vers
    // SqlChallenges avec suppression en cascade. Null pour un défi de code.
    public int? SqlChallengeId { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
