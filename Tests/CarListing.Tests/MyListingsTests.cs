using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Business.Concrete;
using Entry.Concrete;
using Entry.Controllers;
using Entry.Data;
using Entry.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CarListing.Tests;

/// <summary>GET /api/cars/mine and the deleted-vs-deactivated split, over HTTP.</summary>
public class MyListingsTests
{
    private const int OwnerId = 1;
    private const int OtherId = 2;
    private const int AdminId = 3;

    private const int ActiveCarId = 20;       // owner, 1h old, has a main image
    private const int DeactivatedCarId = 21;  // owner, 2h old, admin turned it off
    private const int DeletedCarId = 22;      // owner, newest, owner deleted it
    private const int OtherUsersCarId = 23;   // someone else's
    private const int OldActiveCarId = 24;    // owner, 3h old, no images

    [Fact]
    public async Task Mine_Is401WithoutToken()
    {
        await using var factory = await CreateSeededFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/cars/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Mine_ReturnsOwnNonDeletedCars_NewestFirst_WithStatus()
    {
        await using var factory = await CreateSeededFactory();
        using var client = ClientFor(factory, OwnerId);

        var page = await client.GetFromJsonAsync<PagedResultDto<MyCarListItemDto>>("/api/cars/mine");

        Assert.Equal(3, page!.Total);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal([ActiveCarId, DeactivatedCarId, OldActiveCarId], page.Items.Select(i => i.Id));
        Assert.Equal(["Active", "Deactivated", "Active"], page.Items.Select(i => i.Status));
        Assert.Equal("https://img.test/main.jpg", page.Items[0].MainImageUrl);
        Assert.Null(page.Items[2].MainImageUrl);
        Assert.Equal("ABS", Assert.Single(page.Items[0].Features).Key);
    }

    [Fact]
    public async Task Mine_SerializesStatusAsString()
    {
        await using var factory = await CreateSeededFactory();
        using var client = ClientFor(factory, OwnerId);

        var json = await client.GetStringAsync("/api/cars/mine");

        Assert.Contains("\"status\":\"Deactivated\"", json);
        Assert.Contains("\"mainImageUrl\":", json);
    }

    [Theory]
    [InlineData("Active", new[] { ActiveCarId, OldActiveCarId })]
    [InlineData("deactivated", new[] { DeactivatedCarId })]
    public async Task Mine_FiltersByStatus_CaseInsensitive(string status, int[] expectedIds)
    {
        await using var factory = await CreateSeededFactory();
        using var client = ClientFor(factory, OwnerId);

        var page = await client.GetFromJsonAsync<PagedResultDto<MyCarListItemDto>>($"/api/cars/mine?status={status}");

        Assert.Equal(expectedIds, page!.Items.Select(i => i.Id));
        Assert.Equal(expectedIds.Length, page.Total);
    }

    [Fact]
    public async Task Mine_UnknownStatus_Is400WithMessage()
    {
        await using var factory = await CreateSeededFactory();
        using var client = ClientFor(factory, OwnerId);

        var response = await client.GetAsync("/api/cars/mine?status=Deleted");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MessageResponseDto>();
        Assert.Equal(CarsController.InvalidStatusMessage, body!.Message);
    }

    [Fact]
    public async Task Mine_PagesAndCapsPageSizeAt50()
    {
        await using var factory = await CreateSeededFactory();
        using var client = ClientFor(factory, OwnerId);

        var second = await client.GetFromJsonAsync<PagedResultDto<MyCarListItemDto>>("/api/cars/mine?page=2&pageSize=2");
        var capped = await client.GetFromJsonAsync<PagedResultDto<MyCarListItemDto>>("/api/cars/mine?pageSize=500");

        Assert.Equal([OldActiveCarId], second!.Items.Select(i => i.Id));
        Assert.Equal(3, second.Total);
        Assert.Equal(50, capped!.PageSize);
    }

    [Fact]
    public async Task Delete_MarksCarDeleted_NotDeactivated()
    {
        await using var factory = await CreateSeededFactory();
        using var owner = ClientFor(factory, OwnerId);
        using var admin = ClientFor(factory, AdminId);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/cars/{ActiveCarId}")).StatusCode);

        var car = await ReadCar(factory, ActiveCarId);
        Assert.NotNull(car.DeletedAt);
        Assert.False(car.IsActive);

        var mine = await owner.GetFromJsonAsync<PagedResultDto<MyCarListItemDto>>("/api/cars/mine");
        Assert.DoesNotContain(mine!.Items, i => i.Id == ActiveCarId);

        // Gone for everyone, owner and admin included.
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/cars/{ActiveCarId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/cars/{ActiveCarId}")).StatusCode);
    }

    [Fact]
    public async Task Delete_WorksOnADeactivatedCar()
    {
        await using var factory = await CreateSeededFactory();
        using var owner = ClientFor(factory, OwnerId);

        var response = await owner.DeleteAsync($"/api/cars/{DeactivatedCarId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotNull((await ReadCar(factory, DeactivatedCarId)).DeletedAt);
    }

    [Fact]
    public async Task Delete_AlreadyDeletedCar_Is404()
    {
        await using var factory = await CreateSeededFactory();
        using var owner = ClientFor(factory, OwnerId);

        var response = await owner.DeleteAsync($"/api/cars/{DeletedCarId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdminActivate_DoesNotUndeleteADeletedCar()
    {
        await using var factory = await CreateSeededFactory();
        using var admin = ClientFor(factory, AdminId);

        var response = await admin.PutAsync($"/api/admin/cars/{DeletedCarId}/active?active=true", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var car = await ReadCar(factory, DeletedCarId);
        Assert.False(car.IsActive);
        Assert.NotNull(car.DeletedAt);
    }

    [Fact]
    public async Task AdminToggle_OnlyChangesDeactivation()
    {
        await using var factory = await CreateSeededFactory();
        using var admin = ClientFor(factory, AdminId);
        using var owner = ClientFor(factory, OwnerId);

        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.PutAsync($"/api/admin/cars/{ActiveCarId}/active?active=false", null)).StatusCode);

        var car = await ReadCar(factory, ActiveCarId);
        Assert.False(car.IsActive);
        Assert.Null(car.DeletedAt);

        var mine = await owner.GetFromJsonAsync<PagedResultDto<MyCarListItemDto>>("/api/cars/mine");
        Assert.Equal("Deactivated", mine!.Items.Single(i => i.Id == ActiveCarId).Status);

        // Unchanged: owner still sees it, with isActive false; the public does not.
        var detail = await owner.GetAsync($"/api/cars/{ActiveCarId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Contains("\"isActive\":false", await detail.Content.ReadAsStringAsync());
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/cars/{ActiveCarId}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.PutAsync($"/api/admin/cars/{ActiveCarId}/active?active=true", null)).StatusCode);
        Assert.True((await ReadCar(factory, ActiveCarId)).IsActive);
    }

    // ---- setup --------------------------------------------------------------

    private static async Task<Car> ReadCar(CarListingApiFactory factory, int id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Cars.AsNoTracking().SingleAsync(c => c.Id == id);
    }

    private static async Task<CarListingApiFactory> CreateSeededFactory()
    {
        var factory = new CarListingApiFactory();
        var now = DateTime.UtcNow;
        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                NewUser(OwnerId, "owner", "User"),
                NewUser(OtherId, "other", "User"),
                NewUser(AdminId, "admin", "Admin"));

            db.Cars.AddRange(
                NewCar(ActiveCarId, OwnerId, isActive: true, createdAt: now.AddHours(-1)),
                NewCar(DeactivatedCarId, OwnerId, isActive: false, createdAt: now.AddHours(-2)),
                NewCar(DeletedCarId, OwnerId, isActive: false, createdAt: now, deletedAt: now),
                NewCar(OtherUsersCarId, OtherId, isActive: true, createdAt: now),
                NewCar(OldActiveCarId, OwnerId, isActive: true, createdAt: now.AddHours(-3)));

            db.CarImages.AddRange(
                new CarImage { Id = 201, CarId = ActiveCarId, ImageUrl = "https://img.test/other.jpg", Order = 0 },
                new CarImage { Id = 202, CarId = ActiveCarId, ImageUrl = "https://img.test/main.jpg", Order = 1, IsMain = true });

            db.CarFeatures.Add(new CarFeature { Id = 301, CarId = ActiveCarId, Key = "ABS", Value = "Var" });

            return Task.CompletedTask;
        });
        return factory;
    }

    private static HttpClient ClientFor(CarListingApiFactory factory, int userId)
    {
        var user = userId switch
        {
            OwnerId => NewUser(OwnerId, "owner", "User"),
            OtherId => NewUser(OtherId, "other", "User"),
            AdminId => NewUser(AdminId, "admin", "Admin"),
            _ => throw new ArgumentOutOfRangeException(nameof(userId))
        };

        using var scope = factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<TokenService>().CreateToken(user);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static User NewUser(int id, string name, string role) => new()
    {
        Id = id,
        Username = name,
        Email = $"{name}@test.local",
        PasswordHash = "hash",
        Role = role
    };

    private static Car NewCar(int id, int userId, bool isActive, DateTime createdAt, DateTime? deletedAt = null) => new()
    {
        Id = id,
        Title = $"Car {id}",
        Brand = "Toyota",
        Model = "Camry",
        Year = 2020,
        Price = 15_000,
        Mileage = 80_000,
        FuelType = "Petrol",
        BodyType = "Sedan",
        Transmission = "Automatic",
        Engine = "2.0",
        City = "Baku",
        ContactPhone = "+994501234567",
        UserId = userId,
        IsActive = isActive,
        DeletedAt = deletedAt,
        CreatedAt = createdAt
    };
}
