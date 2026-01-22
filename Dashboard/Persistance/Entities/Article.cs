using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboard.Persistance.Entities;

public class Article
{
    public int Id { get; set; }

    [Required, StringLength(160)]
    public string Titre { get; set; } = default!;

    [Required, DataType(DataType.MultilineText)]
    public string Contenu { get; set; } = default!;

    public DateTime DateCreation { get; set; } = DateTime.UtcNow;

    public string? AuthorId { get; set; }

    public bool IsPublic { get; set; }

    [ForeignKey(nameof(AuthorId))]
    public IdentityUser? Author { get; set; }

    public ICollection<Label> Labels { get; set; } = new List<Label>();

    public Guid? CoverMediaId { get; set; }
    public MediaAsset? CoverMedia { get; set; }
}