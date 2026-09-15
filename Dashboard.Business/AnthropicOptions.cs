namespace Dashboard.Business;

/// <summary>
/// Reglages de l'analyse des mails par Claude. La cle ne vit jamais dans appsettings.json :
/// variable d'environnement Anthropic__Key (fichier .env).
/// Les plafonds sont des garde-fous contre un bug qui ferait tourner la facture : le credit
/// prepaye et le plafond de la Console Anthropic restent la protection de dernier recours.
/// </summary>
public class AnthropicOptions
{
    public string Key { get; set; } = "";

    public string Model { get; set; } = "claude-opus-5";

    /// <summary>low / medium / high. Classer un mail est simple : low suffit et coute le moins.</summary>
    public string Effort { get; set; } = "low";

    public int MaxTokens { get; set; } = 4096;

    /// <summary>Au-dela, le corps du mail est coupe : un mail de candidature tient largement dedans.</summary>
    public int MaxBodyChars { get; set; } = 12000;

    public int MaxAnalysesPerRun { get; set; } = 50;

    /// <summary>Nombre d'appels factures par jour (UTC), echecs compris.</summary>
    public int MaxCallsPerDay { get; set; } = 200;

    /// <summary>L'analyse s'arrete quand le cout estime du mois (UTC) atteint ce montant.</summary>
    public decimal MonthlyBudgetUsd { get; set; } = 5m;

    public int MaxAttemptsPerEmail { get; set; } = 2;

    /// <summary>Tarifs en dollars par million de tokens, pour l'estimation du cout.</summary>
    public decimal InputPricePerMTok { get; set; } = 5m;
    public decimal OutputPricePerMTok { get; set; } = 25m;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Key);
}
