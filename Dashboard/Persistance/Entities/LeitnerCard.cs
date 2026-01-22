using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboard.Persistance.Entities;

public class LeitnerCard
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string OwnerId { get; set; } = string.Empty;

    [Required]
    public int QuizQuestionId { get; set; }

    [ForeignKey(nameof(QuizQuestionId))]
    public QuestionTechnique? Question { get; set; }

    [Range(1, 20)]
    public int Box { get; set; } = 1;

    // Date (UTC) à laquelle la question doit réapparaître
    public DateOnly NextDueDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public DateTime? LastReviewedUtc { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
