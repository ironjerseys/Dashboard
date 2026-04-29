namespace Dashboard.Persistance.Entities;

public class JobPosting
{
    public int Id { get; set; }
    public string Site { get; set; } = "";
    public string JobUrl { get; set; } = "";
    public string? JobUrlDirect { get; set; }
    public string Title { get; set; } = "";
    public string? Company { get; set; }
    public string? Location { get; set; }
    public DateTime? DatePosted { get; set; }
    public string? JobType { get; set; }
    public string? Interval { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public string? Currency { get; set; }
    public bool? IsRemote { get; set; }
    public string? JobLevel { get; set; }
    public string SearchRole { get; set; } = "";
    public string SearchCity { get; set; } = "";
    public DateTime ScrapedAt { get; set; }
}
