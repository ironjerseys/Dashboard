namespace Dashboard.Persistance.Entities;

public class MediaAsset

{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string ContentType { get; set; } = "image/png";
    public string FileName { get; set; } = "image";

    public byte[] Data { get; set; } = Array.Empty<byte>();

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    // Optionnel (dédup plus tard)
    public string? Sha256 { get; set; }
}
