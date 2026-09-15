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

    /// <summary>Libelle Gmail rempli par le filtre de candidatures (un libelle = un dossier IMAP).</summary>
    public string Folder { get; set; } = "Applications";

    /// <summary>Libelle pose sur un mail une fois analyse.</summary>
    public string ProcessedLabel { get; set; } = "Applications/Processed";

    /// <summary>
    /// Sortir aussi le mail analyse de la boite de reception. Desactive par defaut : l'analyse
    /// tourne toute seule, un mail d'entretien disparaitrait de l'INBOX avant d'avoir ete vu.
    /// </summary>
    public bool ArchiveProcessed { get; set; }

    /// <summary>Intervalle du passage automatique (synchro, analyse, libelles). 0 le desactive.</summary>
    public int PipelineIntervalMinutes { get; set; } = 30;

    /// <summary>Nombre de lots de synchro enchaines dans un passage quand il reste des mails a recuperer.</summary>
    public int MaxSyncBatchesPerRun { get; set; } = 5;

    /// <summary>Plafond de mails rapatries en un passage, pour ne pas saturer un premier run.</summary>
    public int MaxMessagesPerRun { get; set; } = 200;

    /// <summary>Au tout premier passage, on ne remonte pas plus loin que ce nombre de jours.</summary>
    public int InitialLookbackDays { get; set; } = 90;
}
