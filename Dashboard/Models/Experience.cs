namespace Dashboard.Models;

public class Experience
{
    public string Titre { get; set; }
    public string LogoEntreprise { get; set; }
    public string Entreprise { get; set; }
    public string Periode { get; set; }
    // Contient du HTML contrôlé → sera rendu avec Html.Raw dans la vue
    public string Description { get; set; }
    public List<string> Technos { get; set; } = new();

    public string? LogoUrl { get; set; }
}
