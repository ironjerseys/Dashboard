using System.ComponentModel.DataAnnotations;

namespace Dashboard.Models;

public class Todo
{
    public int Id { get; set; }

    [Required, MaxLength(500)]
    public string Description { get; set; } = string.Empty;
}
