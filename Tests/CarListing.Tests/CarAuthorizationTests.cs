using System.Security.Claims;
using Entry.Concrete;
using Entry.Controllers;
using Entry.Data;
using Entry.Dto;
using Entry.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarListing.Tests;

public class CarAuthorizationTests
{
    [Fact]
    public async Task Update_ReturnsForbid_WhenUserDoesNotOwnCar()
    {
        await using var db = CreateDbContext();
        await SeedOwnerAndCar(db);

        var controller = CreateController(db, userId: 2);

        var result = await controller.Update(1, new UpdateCarDto
        {
            Title = "Updated",
            Brand = "Toyota",
            Model = "Camry",
            Year = 2020,
            Price = 15_000,
            Mileage = 80_000,
            FuelType = "Petrol",
            BodyType = "Sedan",
            Transmission = "Automatic",
            Engine = "2.0",
            City = "Baku"
        });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsForbid_WhenUserDoesNotOwnCar()
    {
        await using var db = CreateDbContext();
        await SeedOwnerAndCar(db);

        var controller = CreateController(db, userId: 2);

        var result = await controller.Delete(1);

        Assert.IsType<ForbidResult>(result);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static async Task SeedOwnerAndCar(AppDbContext db)
    {
        db.Users.AddRange(
            new User
            {
                Id = 1,
                Username = "owner",
                Email = "owner@test.local",
                PasswordHash = "hash",
                Role = "User"
            },
            new User
            {
                Id = 2,
                Username = "other",
                Email = "other@test.local",
                PasswordHash = "hash",
                Role = "User"
            });

        db.Cars.Add(new Car
        {
            Id = 1,
            Title = "Original",
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
            IsActive = true
        });

        await db.SaveChangesAsync();
    }

    private static CarsController CreateController(AppDbContext db, int userId)
    {
        var controller = new CarsController(db, new FakeImageStorageService(), new RuleBasedAiSearchParserService());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Role, "User")
                ], "TestAuth"))
            }
        };

        return controller;
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
