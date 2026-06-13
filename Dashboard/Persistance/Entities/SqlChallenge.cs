using System.ComponentModel.DataAnnotations;

namespace Dashboard.Persistance.Entities;

// Défi SQL : l'utilisateur saisit une requête SELECT qui est exécutée
// dans une base SQLite en mémoire (jetable, isolée de la vraie base).
// Les questions sont stockées en base pour pouvoir être ajoutées par script.
public class SqlChallenge
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    // Énoncé montré à l'utilisateur (ex : "Sélectionne tous les utilisateurs de plus de 30 ans").
    [Required]
    public string Prompt { get; set; } = string.Empty;

    // Script de création du schéma (CREATE TABLE ...), exécuté dans la base jetable.
    [Required]
    public string SchemaSql { get; set; } = string.Empty;

    // Script d'insertion des données de test (INSERT ...).
    public string SeedSql { get; set; } = string.Empty;

    // Requête solution de référence : exécutée côté serveur pour calculer
    // le résultat attendu, jamais montrée à l'utilisateur.
    [Required]
    public string SolutionSql { get; set; } = string.Empty;

    // Si vrai, l'ordre des lignes du résultat doit correspondre exactement
    // (utile quand l'énoncé impose un ORDER BY).
    public bool OrderMatters { get; set; }

    // Indice optionnel affiché à la demande.
    public string? Hint { get; set; }

    // Pour trier l'affichage de la liste.
    public int SortOrder { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
