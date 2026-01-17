using Dashboard.Data;
using Dashboard.Entities;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Services;

public sealed record MediaItem(Guid Id, string FileName, string ContentType, DateTime CreatedUtc)
{
    public string Url => $"/media/{Id}";
}

public interface IMediaLibraryService
{
    Task<Guid> UploadAsync(IBrowserFile file, long maxBytes = 2 * 1024 * 1024, CancellationToken ct = default);
    Task<List<MediaItem>> GetLatestAsync(int take = 30, CancellationToken ct = default);
}

public sealed class MediaLibraryService : IMediaLibraryService
{
    private readonly IDbContextFactory<BlogContext> _dbContextFactory;

    public MediaLibraryService(IDbContextFactory<BlogContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Guid> UploadAsync(IBrowserFile file, long maxBytes = 2 * 1024 * 1024, CancellationToken ct = default)
    {
        if (file is null) throw new ArgumentNullException(nameof(file));
        if (string.IsNullOrWhiteSpace(file.ContentType) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Le fichier doit être une image.");

        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        await using var stream = file.OpenReadStream(maxBytes, ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);

        var media = new MediaAsset
        {
            ContentType = file.ContentType,
            FileName = file.Name,
            Data = ms.ToArray(),
            CreatedUtc = DateTime.UtcNow
        };

        db.MediaAssets.Add(media);
        await db.SaveChangesAsync(ct);

        return media.Id;
    }

    public async Task<List<MediaItem>> GetLatestAsync(int take = 30, CancellationToken ct = default)
    {
        take = take <= 0 ? 30 : Math.Min(take, 100);

        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        return await db.MediaAssets.AsNoTracking()
            .OrderByDescending(x => x.CreatedUtc)
            .Take(take)
            .Select(x => new MediaItem(x.Id, x.FileName, x.ContentType, x.CreatedUtc))
            .ToListAsync(ct);
    }
}
