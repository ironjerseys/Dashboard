using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboard.Models;

public class Goal
{
    public int Id { get; set; }

    [Required, MaxLength(160)]
    public string Titre { get; set; } = string.Empty; // display title

    [MaxLength(2000)]
    public string? Description { get; set; }

    // Identifie la semaine (lundi) pour laquelle s'applique l'objectif
    [DataType(DataType.Date)]
    public DateOnly WeekStart { get; set; }

    // Lien optionnel vers un article (compte-rendu)
    public int? ArticleId { get; set; }
    public Article? Article { get; set; }

    // Statut d'atteinte de l'objectif sur la semaine
    public bool IsDone { get; set; }

    // Propriétaire
    [MaxLength(450)]
    public string? OwnerId { get; set; }
}
