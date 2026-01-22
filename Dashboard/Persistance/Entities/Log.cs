using System.ComponentModel.DataAnnotations;

namespace Dashboard.Persistance.Entities;

public class Log
{
    public int Id { get; set; }

    [Required]
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(32)]
    public string Level { get; set; } = "Info"; // Info, Warn, Error, Debug

    [MaxLength(128)]
    public string Source { get; set; } = string.Empty; // e.g., GoalReminderService, SmtpEmailSender

    [MaxLength(128)]
    public string? Event { get; set; } // e.g., SendingEmail, EmailSent, EmailFailed

    [MaxLength(2048)]
    public string? Message { get; set; }

    // JSON or text payload for additional context (recipient, counts, etc.)
    public string? Data { get; set; }

    [MaxLength(1024)]
    public string? Exception { get; set; }
}
