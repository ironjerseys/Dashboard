using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboard.Persistance.Entities;

public class Goal
{
    public int Id { get; set; }

    [Required, MaxLength(160)]
    public string Titre { get; set; } = string.Empty; // display title

    [MaxLength(2000)]
    public string? Description { get; set; }

    // Nouvelle période: Début et Fin (inclus)
    [DataType(DataType.Date)]
    public DateOnly Debut { get; set; }

    [DataType(DataType.Date)]
    public DateOnly Fin { get; set; }

    // Lien optionnel vers un article (compte-rendu)
    public int? ArticleId { get; set; }
    public Article? Article { get; set; }

    // Statut d'atteinte de l'objectif sur la période
    public bool IsDone { get; set; }

    // Propriétaire
    [MaxLength(450)]
    public string? OwnerId { get; set; }
}
