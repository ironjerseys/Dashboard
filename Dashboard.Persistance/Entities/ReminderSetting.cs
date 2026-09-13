using System.ComponentModel.DataAnnotations;

namespace Dashboard.Persistance.Entities;

/// <summary>
/// Preferences d'envoi du mail de rappel quotidien, par utilisateur.
/// Un utilisateur sans ligne garde le comportement par defaut : actif, 18h, adresse du compte.
/// </summary>
public class ReminderSetting
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string OwnerId { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    /// <summary>Heure d'envoi (0-23), comparee a l'heure locale du serveur.</summary>
    [Range(0, 23)]
    public int HourLocal { get; set; } = DefaultHourLocal;

    /// <summary>Adresse de remplacement. Null ou vide => adresse du compte Identity.</summary>
    [MaxLength(256)]
    public string? EmailOverride { get; set; }

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public const int DefaultHourLocal = 18;
}
