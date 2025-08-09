using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Dashboard.Models;

public class Article
{
    public int Id { get; set; }

    [Required, StringLength(160)]
    public string Titre { get; set; } = default!;

    [Required, DataType(DataType.MultilineText)]
    public string Contenu { get; set; } = default!;

    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
    
    public string? AuthorId { get; set; } 
    
    [ForeignKey(nameof(AuthorId))]
    public IdentityUser? Author { get; set; }
}