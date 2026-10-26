using System.ComponentModel.DataAnnotations;

namespace Dashboard.Persistance.Entities;

// Défi (code ou SQL) désactivé par un administrateur, identifié par sa clé
// ("singleton", "two-sum", "sql-12"...). Absent de la table = actif.
// Un défi désactivé n'apparaît plus dans les listes ni dans les compteurs "dus".
public class InactiveChallenge
{
    [Key, MaxLength(100)]
    public string ChallengeKey { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
