using System.ComponentModel.DataAnnotations;

namespace Dashboard.Models;

public class GoalFormModel
{
    public int? Id { get; set; }

    [Required]
    public string Titre { get; set; } = "";

    public string? Description { get; set; }

    [Required]
    public DateOnly Debut { get; set; }

    [Required]
    public DateOnly Fin { get; set; }

    public int? ArticleId { get; set; }
    public bool IsDone { get; set; }
}
