namespace Dashboard.Entities;

public class Quantifier
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<QuantifierEntry> Entries { get; set; } = new();
}
