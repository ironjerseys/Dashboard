using System.ComponentModel.DataAnnotations;

namespace Dashboard.Entities;

public enum EmailFrequency
{
    Daily,
    Weekly,
    Monthly
}

public class EmailSettings
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string RecipientEmail { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public EmailFrequency Frequency { get; set; } = EmailFrequency.Daily;

    [Range(0, 23)]
    public int Hour { get; set; } = 9;

    [Range(0, 59)]
    public int Minute { get; set; } = 0;

    // For weekly
    public DayOfWeek? DayOfWeek { get; set; }

    // For monthly (1-31)
    [Range(1, 31)]
    public int? DayOfMonth { get; set; }

    public DateTime? LastSentUtc { get; set; }

    public bool IncludeTodos { get; set; }
    public bool IncludeGoals { get; set; }
    public bool IncludeArticles { get; set; }
}