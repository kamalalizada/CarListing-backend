using Entry.Data;
using Microsoft.EntityFrameworkCore;

namespace Entry.Services;

public sealed class InactiveCarImageCleanupService
{
    private readonly AppDbContext _db;
    private readonly IImageStorageService _imageStorage;

    public InactiveCarImageCleanupService(AppDbContext db, IImageStorageService imageStorage)
    {
        _db = db;
        _imageStorage = imageStorage;
    }

    public async Task<int> PurgeAsync(CancellationToken cancellationToken = default)
    {
        var images = await _db.CarImages
            .Include(i => i.Car)
            // Deleted cars only. A deactivated car can be re-activated by an
            // admin, and its photos must still be there when it is.
            .Where(i => i.Car.DeletedAt != null)
            .ToListAsync(cancellationToken);

        foreach (var image in images)
        {
            var objectKey = image.ObjectKey ?? _imageStorage.TryGetObjectKeyFromUrl(image.ImageUrl);
            await _imageStorage.DeleteAsync(objectKey, cancellationToken);
        }

        _db.CarImages.RemoveRange(images);
        await _db.SaveChangesAsync(cancellationToken);

        return images.Count;
    }
}
