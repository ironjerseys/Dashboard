using System.ComponentModel.DataAnnotations;

namespace Dashboard.Persistance.Entities;

/// <summary>
/// Un appel facture a l'API Claude. Sert de compteur pour les plafonds (par jour, budget du mois)
/// et d'historique de cout ; on l'enregistre aussi quand la reponse est inexploitable.
/// </summary>
public class AiUsageRecord
{
    public int Id { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(64)]
    public string Model { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string Purpose { get; set; } = string.Empty;

    public int? EmailMessageId { get; set; }

    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long CacheCreationInputTokens { get; set; }
    public long CacheReadInputTokens { get; set; }

    /// <summary>Estimation a partir des tarifs configures ; la Console Anthropic fait foi.</summary>
    public decimal EstimatedCostUsd { get; set; }
}
