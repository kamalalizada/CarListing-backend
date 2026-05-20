using Entry.Concrete;
using Entry.Controllers;
using Entry.Data;
using Entry.Dto;
using Entry.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarListing.Tests;

public class CarSearchTests
{
    [Fact]
    public async Task GetAll_FiltersByPriceYearAndMileage()
    {
        await using var db = CreateDbContext();
        await SeedCars(db);
        var controller = CreateController(db);

        var result = await controller.GetAll(new CarSearchFilterDto
        {
            PriceMin = 10_000,
            PriceMax = 20_000,
            YearMin = 2019,
            MileageMax = 90_000
        });

        var page = AssertPage(result);
        var car = Assert.Single(page.Items);
        Assert.Equal("Camry", car.Model);
    }

    [Fact]
    public async Task GetAll_FiltersByStructuredFields()
    {
        await using var db = CreateDbContext();
        await SeedCars(db);
        var controller = CreateController(db);

        var result = await controller.GetAll(new CarSearchFilterDto
        {
            City = "Baku",
            FuelType = "Hybrid",
            BodyType = "SUV",
            Transmission = "Automatic"
        });

        var page = AssertPage(result);
        var car = Assert.Single(page.Items);
        Assert.Equal("RAV4", car.Model);
    }

    [Fact]
    public async Task GetAll_SearchesTextAcrossCoreFields()
    {
        await using var db = CreateDbContext();
        await SeedCars(db);
        var controller = CreateController(db);

        var result = await controller.GetAll(new CarSearchFilterDto
        {
            Search = "Civic"
        });

        var page = AssertPage(result);
        var car = Assert.Single(page.Items);
        Assert.Equal("Honda", car.Brand);
    }

    [Fact]
    public async Task GetAll_SortsByMileageAscending()
    {
        await using var db = CreateDbContext();
        await SeedCars(db);
        var controller = CreateController(db);

        var result = await controller.GetAll(new CarSearchFilterDto
        {
            SortBy = "mileage",
            SortDirection = "asc"
        });

        var page = AssertPage(result);
        Assert.Equal(["RAV4", "Camry", "Civic"], page.Items.Select(i => i.Model).ToArray());
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

    private static PagedResultDto<CarListItemDto> AssertPage(ActionResult<PagedResultDto<CarListItemDto>> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<PagedResultDto<CarListItemDto>>(ok.Value);
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
                Title = "Clean Toyota Camry",
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
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new Car
            {
                Id = 2,
                Title = "Family Toyota RAV4",
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
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new Car
            {
                Id = 3,
                Title = "Honda Civic manual",
                Brand = "Honda",
                Model = "Civic",
                Year = 2018,
                Price = 15_000,
                Mileage = 120_000,
                FuelType = "Petrol",
                BodyType = "Sedan",
                Transmission = "Manual",
                Engine = "1.8",
                City = "Ganja",
                UserId = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
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
