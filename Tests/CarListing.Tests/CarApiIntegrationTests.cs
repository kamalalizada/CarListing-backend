using System.Net;
using System.Net.Http.Json;
using Entry.Concrete;
using Entry.Dto;

namespace CarListing.Tests;

public class CarApiIntegrationTests
{
    [Fact]
    public async Task Register_ReturnsToken()
    {
        await using var factory = new CarListingApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterDto
        {
            Username = "kamal",
            Email = "kamal@test.local",
            Password = "secret123"
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        Assert.False(string.IsNullOrWhiteSpace(body?.Token));
    }

    [Fact]
    public async Task GetCars_AppliesFilterOverHttp()
    {
        await using var factory = new CarListingApiFactory();
        await factory.SeedAsync(db =>
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
                CreateCar(1, "Toyota", "Camry", 18_000, 2020, 80_000, "Sedan", "Petrol", "Automatic", "Baku"),
                CreateCar(2, "Honda", "Civic", 15_000, 2018, 120_000, "Sedan", "Petrol", "Manual", "Ganja"));

            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/cars?brand=Toyota&priceMax=20000&yearMin=2019&mileageMax=90000");

        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResultDto<CarListItemDto>>();

        var car = Assert.Single(page!.Items);
        Assert.Equal("Camry", car.Model);
    }

    [Fact]
    public async Task Compare_ReturnsBadRequest_WhenOneCarIsMissing()
    {
        await using var factory = new CarListingApiFactory();
        await factory.SeedAsync(db =>
        {
            db.Users.Add(new User
            {
                Id = 1,
                Username = "seller",
                Email = "seller@test.local",
                PasswordHash = "hash",
                Role = "User"
            });

            db.Cars.Add(CreateCar(1, "Toyota", "Camry", 18_000, 2020, 80_000, "Sedan", "Petrol", "Automatic", "Baku"));
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/cars/compare?ids=1&ids=99");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Car CreateCar(
        int id,
        string brand,
        string model,
        decimal price,
        int year,
        int mileage,
        string bodyType,
        string fuelType,
        string transmission,
        string city)
    {
        return new Car
        {
            Id = id,
            Title = $"{brand} {model}",
            Brand = brand,
            Model = model,
            Year = year,
            Price = price,
            Mileage = mileage,
            BodyType = bodyType,
            FuelType = fuelType,
            Transmission = transmission,
            Engine = "2.0",
            City = city,
            UserId = 1,
            IsActive = true
        };
    }
}
