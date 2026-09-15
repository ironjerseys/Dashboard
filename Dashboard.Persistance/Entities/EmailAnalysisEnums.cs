namespace Dashboard.Persistance.Entities;

/// <summary>Ou en est l'analyse automatique d'un mail.</summary>
public enum EmailAnalysisState
{
    /// <summary>Recupere, pas encore analyse.</summary>
    Pending,

    Analyzed,

    /// <summary>Toutes les tentatives ont echoue ; seul un clic "Reessayer" le remet en file.</summary>
    Failed
}

/// <summary>Ce que le mail dit de la candidature, tel que determine par l'analyse.</summary>
public enum EmailEventType
{
    ApplicationReceived,
    Assessment,
    Interview,
    Offer,
    Rejection,

    /// <summary>Concerne une candidature mais ne change pas son statut.</summary>
    OtherUpdate,

    /// <summary>Alerte emploi, newsletter, securite du compte... rien a suivre.</summary>
    NotApplication
}
