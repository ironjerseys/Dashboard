namespace Dashboard.DTO;

public sealed record MediaListItemDto(Guid Id, string FileName, string ContentType, DateTime CreatedUtc, string Url);
