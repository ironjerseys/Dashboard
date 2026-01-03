namespace Dashboard.Entities;

public class QuantifierEntry
{
    public int Id { get; set; }

    public int QuantifierId { get; set; }
    public Quantifier Quantifier { get; set; } = default!;

    public DateOnly Date { get; set; }
    public decimal Value { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
