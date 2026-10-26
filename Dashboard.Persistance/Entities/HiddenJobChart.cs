using System.ComponentModel.DataAnnotations;

namespace Dashboard.Persistance.Entities;

// Graphe masqué sur la page /job-data (pour tous les visiteurs). Absent de la table = affiché.
public class HiddenJobChart
{
    [Key, MaxLength(64)]
    public string ChartKey { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
