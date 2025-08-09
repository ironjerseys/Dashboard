using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Dashboard.Models;

namespace Dashboard.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult CV()
    {
        var experiences = new List<Experience>
            {
                new Experience
                {
                    Titre = "Développeur .NET",
                    LogoEntreprise = string.Empty,
                    Entreprise = "Adentis",
                    Periode = "Mars 2024 - Maintenant",
                    Description = "<p>J'ai travaillé comme développeur .NET pour le Crédit Agricole dans le cadre d'une refonte d'un grand nombre d'applications à réécrire en C# .NET 8.0</p>",
                    Technos = new List<string>{ "C#", ".NET Core", "ASP.NET", "GitLab", "CI/CD", "SQL Server" }
                },
                new Experience
                {
                    Titre = "Développeur .NET",
                    LogoEntreprise = string.Empty,
                    Entreprise = "Alten",
                    Periode = "Octobre 2024 - Février 2024",
                    Description = "<p>J'ai travaillé comme développeur .NET à la Direction des Systèmes d'Information pour Doxallia, filiale du Crédit Agricole, principalement sur une API C# .NET 8.0 avec une importante partie DevOps.</p><p>Pour la mise en production de l'API j'ai installé et configuré les serveurs IIS et créé la pipeline CI/CD Azure Devops Server. J'ai aussi participé à la sécurisation du SI, notamment dans le développement et la gestion des flux entre les serveurs de la plateforme sur laquelle je travaillais. Mon travail consistait aussi à maintenir les serveurs applicatifs et serveurs de bases de données de ma plateforme</p>",
                    Technos = new List<string>{ "C#", ".NET Core", "Azure DevOps", "CI/CD", "SQL Server" }
                },
                new Experience
                {
                    Titre = "Développeur .NET",
                    LogoEntreprise = string.Empty,
                    Entreprise = "BPCE-IT",
                    Periode = "Septembre 2022 - Aout 2024",
                    Description = "<p>J'ai travaillé en alternance chez BPCE-IT sur la refonte d’un logiciel pour le helpdesk, de .NET Framework 4.8 vers .NET Core 6.0 et la création d'une nouvelle architecture.</p><p>Le helpdesk BPCE-IT utilise ce logiciel pour effectuer des diagnostics, modifier les parametres de la machine distante, ouvrir un ticket avec des informations préremplies ou prendre la main à distance, et a besoin d'un logiciel à la fois fiable et rapide.</p><p>La codebase était ancienne avec beaucoup de code legacy, le logiciel était en .NET Framework 4.8 et était très buggé. J'ai développé la nouvelle version en .NET Core 6.0. J'ai aussi développé une API RESTful pour s'interfacer avec les bases de données suite à la fermeture des flux, liée à la sécurisation du SI.</p>",
                    Technos = new List<string>{ "C#", ".NET Core", "SQL Server" }
                },
                new Experience
                {
                    Titre = "Master Ingénieur logiciel",
                    LogoEntreprise = string.Empty,
                    Entreprise = "IPI Groupe IGS",
                    Periode = "Septembre 2022 - Aout 2024",
                    Description = string.Empty,
                    Technos = new List<string>{ "C#", ".NET Core", "Python", "Angular", "CI/CD", "SQL Server" }
                },
                new Experience
                {
                    Titre = "Développeur Fullstack",
                    LogoEntreprise = string.Empty,
                    Entreprise = "CREA2F",
                    Periode = "Juin 2021 - Décembre 2021",
                    Description = "<p>J'ai travaillé en CDD chez CREA2F, une agence de communication spécialisée dans les solutions web, en tant que développeur Fullstack Javascript PHP pour créer des sites vitrines et des CMS.</p><p>J'ai eu l'opportunité de travailler sur plusieurs projets pour différents clients. Je gérais à la fois le développement frontend et backend, mais aussi la mise en production. J'ai travaillé avec du Javascript et du PHP.</p><p>Cette expérience a confirmé mon choix de devenir développeur et j'ai finalement décidé de développer mes compétences avec un Master Ingénieur Logiciel.</p>",
                    Technos = new List<string>{ "Angular", "PHP", "MySQL" }
                },
                new Experience
                {
                    Titre = "Bac +3 Développement Backend",
                    LogoEntreprise = string.Empty,
                    Entreprise = "Openclassrooms",
                    Periode = "Janvier 2020 - Aout 2022",
                    Description = string.Empty,
                    Technos = new List<string>{ "PHP", "MySQL", "JavaScript" }
                },
                new Experience
                {
                    Titre = "Analyste Restrictions d'Investissement",
                    LogoEntreprise = string.Empty,
                    Entreprise = "Caceis Luxembourg",
                    Periode = "Janvier 2019 - Décembre 2020",
                    Description = "<p>J'ai travaillé en tant qu'analyste en restrictions d'investissement chez CACEIS, une filiale du Crédit Agricole spécialisée dans les services aux investisseurs.</p><p>Mon rôle d'analyste en restrictions d'investissement consistait à monitorer des portefeuilles financiers grâce à un logiciel spécialisé. Quand des données étaient manquantes il fallait exporter les résultats sur Excel et refaire les calculs avec les nouvelles données. J'ai créé des macros VBA et des scripts Python pour automatiser ces calculs pour gagner du temps mais aussi de la fiabilité.</p><p>Ces automatisations m'ont permis de gagner du temps, ce qui m'a permis de passer plus de temps à programmer, et j'ai finalement décidé d'en faire un travail à temps plein et de devenir développeur.</p>",
                    Technos = new List<string>{ "Python", "VBA" }
                },
                new Experience
                {
                    Titre = "Master Finance de marché - Trading",
                    LogoEntreprise = string.Empty,
                    Entreprise = "ESG Finance Paris",
                    Periode = "Septembre 2016 - Aout 2018",
                    Description = string.Empty,
                    Technos = new List<string>{ "Python", "VBA" }
                }
            };

        return View(experiences);
    }

    public IActionResult Creations()
    {
        return View();
    }

    public IActionResult Articles()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}