using System.ComponentModel.DataAnnotations;

namespace Dashboard.Persistance.Entities;

public class Todo
{
    public int Id { get; set; }

    [Required, MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public bool IsDone { get; set; }
    public DateTime? DoneAt { get; set; }
}
