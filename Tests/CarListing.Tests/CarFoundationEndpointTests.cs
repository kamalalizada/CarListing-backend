using Entry.Concrete;
using Entry.Controllers;
using Entry.Data;
using Entry.Dto;
using Entry.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarListing.Tests;

public class CarFoundationEndpointTests
{
    [Fact]
    public void GetMetadata_ReturnsFrontendValueLists()
    {
        using var db = CreateDbContext();
        var controller = CreateController(db);

        var result = controller.GetMetadata();

        var metadata = AssertOk<CarMetadataDto>(result);
        Assert.Contains("Petrol", metadata.FuelTypes);
        Assert.Contains("SUV", metadata.BodyTypes);
        Assert.Contains("Automatic", metadata.Transmissions);
        Assert.Contains("mileage", metadata.SortOptions);
    }

    [Fact]
    public async Task Compare_ReturnsCarsInRequestedOrder()
    {
        await using var db = CreateDbContext();
        await SeedCars(db);
        var controller = CreateController(db);

        var result = await controller.Compare([2, 1]);

        var items = AssertOk<List<CarCompareItemDto>>(result);
        Assert.Equal([2, 1], items.Select(i => i.Id).ToArray());
    }

    [Fact]
    public async Task Compare_ReturnsBadRequest_WhenLessThanTwoIds()
    {
        await using var db = CreateDbContext();
        var controller = CreateController(db);

        var result = await controller.Compare([1]);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void ParseAiSearch_ReturnsStructuredFilter()
    {
        using var db = CreateDbContext();
        var controller = CreateController(db);

        var result = controller.ParseAiSearch(new AiSearchRequestDto
        {
            Query = "Toyota 18-22 min 2019+ sedan avtomat"
        });

        var parsed = AssertOk<AiSearchResultDto>(result);
        Assert.Equal("Toyota", parsed.Filter.Brand);
        Assert.Equal(18_000, parsed.Filter.PriceMin);
        Assert.Equal(22_000, parsed.Filter.PriceMax);
        Assert.Equal(2019, parsed.Filter.YearMin);
        Assert.Equal("Sedan", parsed.Filter.BodyType);
        Assert.Equal("Automatic", parsed.Filter.Transmission);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static CarsController CreateController(AppDbContext db)
    {
        return new CarsController(db, new FakeImageStorageService(), new RuleBasedAiSearchParserService());
    }

    private static T AssertOk<T>(ActionResult<T> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<T>(ok.Value);
    }

    private static async Task SeedCars(AppDbContext db)
    {
        db.Users.Add(new User
        {
            Id = 1,
            Username = "seller",
            Email = "seller@test.local",
            PasswordHash = "hash",
            Role = "User"
        });

        db.Cars.AddRange(
            new Car
            {
                Id = 1,
                Title = "Toyota Camry",
                Brand = "Toyota",
                Model = "Camry",
                Year = 2020,
                Price = 18_000,
                Mileage = 80_000,
                FuelType = "Petrol",
                BodyType = "Sedan",
                Transmission = "Automatic",
                Engine = "2.0",
                City = "Baku",
                UserId = 1,
                IsActive = true
            },
            new Car
            {
                Id = 2,
                Title = "Toyota RAV4",
                Brand = "Toyota",
                Model = "RAV4",
                Year = 2022,
                Price = 34_000,
                Mileage = 30_000,
                FuelType = "Hybrid",
                BodyType = "SUV",
                Transmission = "Automatic",
                Engine = "2.5",
                City = "Baku",
                UserId = 1,
                IsActive = true
            });

        await db.SaveChangesAsync();
    }

    private sealed class FakeImageStorageService : IImageStorageService
    {
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
            return Task.CompletedTask;
        }

        public string? TryGetObjectKeyFromUrl(string imageUrl)
        {
            return null;
        }
    }
}
