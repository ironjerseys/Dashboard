using System.ComponentModel.DataAnnotations;

namespace Dashboard.Persistance.Entities;

public class LeitnerReview
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string OwnerId { get; set; } = string.Empty;

    [Required]
    public int LeitnerCardId { get; set; }

    public DateTime ReviewedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsCorrect { get; set; }

    public int PreviousBox { get; set; }
    public int NewBox { get; set; }
}
