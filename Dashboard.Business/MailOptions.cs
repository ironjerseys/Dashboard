namespace Dashboard.Business;

/// <summary>
/// Parametres de connexion a la boite mail. Le mot de passe d'application ne doit jamais
/// vivre dans appsettings.json : User Secrets en dev, variable d'environnement en prod.
/// </summary>
public class MailOptions
{
    public string Host { get; set; } = "imap.gmail.com";
    public int Port { get; set; } = 993;
    public bool UseSsl { get; set; } = true;

    /// <summary>Adresse du compte (sert aussi de cle dans MailboxSyncState).</summary>
    public string UserName { get; set; } = "";

    /// <summary>Mot de passe d'application Google, pas le mot de passe du compte.</summary>
    public string Password { get; set; } = "";

    public string Folder { get; set; } = "INBOX";

    /// <summary>Plafond de mails rapatries en un passage, pour ne pas saturer un premier run.</summary>
    public int MaxMessagesPerRun { get; set; } = 200;

    /// <summary>Au tout premier passage, on ne remonte pas plus loin que ce nombre de jours.</summary>
    public int InitialLookbackDays { get; set; } = 90;
}
