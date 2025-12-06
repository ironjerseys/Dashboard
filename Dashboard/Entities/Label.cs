using System.ComponentModel.DataAnnotations;

namespace Dashboard.Entities;

public class Label
{
    public int Id { get; set; }

    [Required, StringLength(64)]
    public string Name { get; set; } = default!;
}
