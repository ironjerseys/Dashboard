namespace Dashboard.DTO;

public sealed record QuantifierCardDto(
     int Id,
     string Name,
     List<QuantifierEntryDto> History
);

public sealed record QuantifierEntryDto(DateOnly Date, int Value);
