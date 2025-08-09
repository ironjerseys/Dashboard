using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Dashboard.Models;

public class Website
{
    public string Name { get; set; }
    public string DescriptionHtml { get; set; } // sera rendu avec Html.Raw
    public string Url { get; set; }             // peut être vide si "retiré"
    public string Image { get; set; }           // chemin relatif (ex: chessmultitool.png)
}