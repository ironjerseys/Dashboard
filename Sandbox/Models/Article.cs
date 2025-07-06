using System.ComponentModel.DataAnnotations;

namespace Sandbox.Models;

public class Article
{
    public int Id { get; set; }

    [Required, StringLength(160)]
    public string Titre { get; set; } = default!;

    [Required, DataType(DataType.MultilineText)]
    public string Contenu { get; set; } = default!;

    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
}