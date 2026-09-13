using System.ComponentModel.DataAnnotations;

namespace Dashboard.Persistance.Entities;

/// <summary>
/// Curseur de synchronisation par boite et par dossier. C'est ce qui evite de re-telecharger
/// toute la boite a chaque passage : on ne demande que les UID superieurs a LastSeenUid.
/// </summary>
public class MailboxSyncState
{
    public int Id { get; set; }

    /// <summary>Adresse du compte synchronise.</summary>
    [Required, MaxLength(320)]
    public string Account { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string Folder { get; set; } = string.Empty;

    /// <summary>
    /// Epoque des UID cote serveur. Si le serveur la change, tous les UID connus deviennent
    /// caducs et il faut repartir de zero (le Message-ID evite les doublons a ce moment-la).
    /// </summary>
    public long UidValidity { get; set; }

    public long LastSeenUid { get; set; }

    public DateTime? LastSyncUtc { get; set; }
}
