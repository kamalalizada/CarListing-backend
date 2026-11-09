using Entry.Concrete;
using Entry.Data;
using Entry.Services;
using Microsoft.EntityFrameworkCore;

namespace CarListing.Tests;

public class InactiveCarImageCleanupServiceTests
{
    [Fact]
    public async Task PurgeAsync_DeletesImagesForDeletedCarsOnly()
    {
        await using var db = CreateDbContext();
        await SeedCarsWithImages(db);
        var imageStorage = new RecordingImageStorageService();
        var service = new InactiveCarImageCleanupService(db, imageStorage);

        var purged = await service.PurgeAsync();

        Assert.Equal(1, purged);
        Assert.Equal(["inactive-key"], imageStorage.DeletedObjectKeys);
        Assert.DoesNotContain(await db.CarImages.ToListAsync(), i => i.ObjectKey == "inactive-key");
        Assert.Contains(await db.CarImages.ToListAsync(), i => i.ObjectKey == "active-key");
        // Deactivated is reversible by an admin, so its photos must survive.
        Assert.Contains(await db.CarImages.ToListAsync(), i => i.ObjectKey == "deactivated-key");
    }

    [Fact]
    public async Task PurgeAsync_DoesNotRemoveDatabaseRows_WhenStorageDeleteFails()
    {
        await using var db = CreateDbContext();
        await SeedCarsWithImages(db);
        var imageStorage = new RecordingImageStorageService { ThrowOnDelete = true };
        var service = new InactiveCarImageCleanupService(db, imageStorage);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PurgeAsync());

        Assert.Contains(await db.CarImages.ToListAsync(), i => i.ObjectKey == "inactive-key");
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static async Task SeedCarsWithImages(AppDbContext db)
    {
        db.Users.Add(new User
        {
            Id = 1,
            Username = "owner",
            Email = "owner@test.local",
            PasswordHash = "hash",
            Role = "User"
        });

        db.Cars.AddRange(
            new Car
            {
                Id = 1,
                Title = "Inactive",
                Brand = "Toyota",
                Model = "Camry",
                Year = 2020,
                Price = 12_000,
                Mileage = 80_000,
                FuelType = "Petrol",
                BodyType = "Sedan",
                Transmission = "Automatic",
                Engine = "2.0",
                City = "Baku",
                UserId = 1,
                IsActive = false,
                DeletedAt = DateTime.UtcNow,
                Images =
                [
                    new CarImage
                    {
                        Id = 1,
                        ImageUrl = "https://test.local/inactive.jpg",
                        ObjectKey = "inactive-key"
                    }
                ]
            },
            new Car
            {
                Id = 2,
                Title = "Active",
                Brand = "Honda",
                Model = "Civic",
                Year = 2021,
                Price = 13_000,
                Mileage = 60_000,
                FuelType = "Petrol",
                BodyType = "Sedan",
                Transmission = "Automatic",
                Engine = "1.5",
                City = "Baku",
                UserId = 1,
                IsActive = true,
                Images =
                [
                    new CarImage
                    {
                        Id = 2,
                        ImageUrl = "https://test.local/active.jpg",
                        ObjectKey = "active-key"
                    }
                ]
            },
            new Car
            {
                Id = 3,
                Title = "Deactivated",
                Brand = "Kia",
                Model = "Rio",
                Year = 2019,
                Price = 9_000,
                Mileage = 90_000,
                FuelType = "Petrol",
                BodyType = "Sedan",
                Transmission = "Manual",
                Engine = "1.4",
                City = "Baku",
                UserId = 1,
                IsActive = false,
                Images =
                [
                    new CarImage
                    {
                        Id = 3,
                        ImageUrl = "https://test.local/deactivated.jpg",
                        ObjectKey = "deactivated-key"
                    }
                ]
            });

        await db.SaveChangesAsync();
    }

    private sealed class RecordingImageStorageService : IImageStorageService
    {
        public List<string?> DeletedObjectKeys { get; } = [];
        public bool ThrowOnDelete { get; set; }

        public Task<ImageStorageResult> UploadAsync(
            int carId,
            Stream stream,
            long length,
            string fileExtension,
            string contentType,
            string? objectKey = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ImageStorageResult("https://test.local/image.jpg", "image.jpg"));
        }

        public Task DeleteAsync(string? objectKey, CancellationToken cancellationToken = default)
        {
            if (ThrowOnDelete)
                throw new InvalidOperationException("Storage delete failed.");

            DeletedObjectKeys.Add(objectKey);
            return Task.CompletedTask;
        }

        public string? TryGetObjectKeyFromUrl(string imageUrl)
        {
            return null;
        }
    }
}
