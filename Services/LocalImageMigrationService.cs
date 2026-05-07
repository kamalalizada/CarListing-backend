using Entry.Data;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace Entry.Services;

public sealed class LocalImageMigrationService
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IImageStorageService _storage;

    public LocalImageMigrationService(
        AppDbContext db,
        IWebHostEnvironment env,
        IImageStorageService storage)
    {
        _db = db;
        _env = env;
        _storage = storage;
    }

    public async Task<int> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var webRoot = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
            webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".webp"] = "image/webp";
        contentTypes.Mappings[".avif"] = "image/avif";

        var localImages = await _db.CarImages
            .Where(i => i.ObjectKey == null && i.ImageUrl.StartsWith("/uploads/"))
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);

        var migrated = 0;
        foreach (var image in localImages)
        {
            var relativePath = image.ImageUrl.TrimStart('/')
                .Replace("/", Path.DirectorySeparatorChar.ToString());
            var physicalPath = Path.Combine(webRoot, relativePath);

            if (!File.Exists(physicalPath))
                continue;

            var fileName = Path.GetFileName(physicalPath);
            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension))
                extension = ".jpg";

            if (!contentTypes.TryGetContentType(fileName, out var contentType))
                contentType = "application/octet-stream";

            await using var stream = File.OpenRead(physicalPath);
            var objectKey = $"cars/{image.CarId}/{fileName}";
            var result = await _storage.UploadAsync(
                image.CarId,
                stream,
                stream.Length,
                extension,
                contentType,
                objectKey,
                cancellationToken);

            image.ImageUrl = result.ImageUrl;
            image.ObjectKey = result.ObjectKey;
            migrated++;
        }

        if (migrated > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return migrated;
    }
}
