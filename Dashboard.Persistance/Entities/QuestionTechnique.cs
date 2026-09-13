using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dashboard.Persistance.Entities;

public class QuestionTechnique
{
    public int Id { get; set; }
    [Required]
    public string QuestionText { get; set; } = string.Empty;
    [Required]
    public string Choice0 { get; set; } = string.Empty;
    [Required]
    public string Choice1 { get; set; } = string.Empty;
    [Required]
    public string Choice2 { get; set; } = string.Empty;
    [Required]
    public string Choice3 { get; set; } = string.Empty;
    [Range(0,3)]
    public int CorrectAnswer { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public bool IsPublic { get; set; } = false;
    public ICollection<Label> Labels { get; set; } = new List<Label>();

    public int? ArticleId { get; set; }
    [ForeignKey(nameof(ArticleId))]
    public Article? Article { get; set; }
}