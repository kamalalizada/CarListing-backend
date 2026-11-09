using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Business.Concrete;
using Entry.Concrete;
using Entry.Dto;
using Microsoft.Extensions.DependencyInjection;

namespace CarListing.Tests;

/// <summary>GET /api/admin/cars and GET /api/admin/users, over HTTP.</summary>
public class AdminListTests
{
    private const int AdminId = 1;
    private const int SellerId = 2;   // "Seller.One", unblocked, 3 cars (1 deleted)
    private const int BlockedId = 3;  // "blocked.guy", blocked, 1 car
    private const int QuietId = 4;    // "quiet", unblocked, no cars

    // ---- auth ---------------------------------------------------------------

    [Theory]
    [InlineData("/api/admin/cars")]
    [InlineData("/api/admin/users")]
    public async Task Lists_Are401WithoutToken(string url)
    {
        await using var factory = await CreateSeededFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/cars")]
    [InlineData("/api/admin/users")]
    public async Task Lists_Are403ForNonAdmin(string url)
    {
        await using var factory = await CreateSeededFactory();
        using var client = ClientFor(factory, SellerId);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
    }

    // ---- cars ---------------------------------------------------------------

    [Fact]
    public async Task Cars_ReturnsEnvelope_NewestFirst_WithOwnerFields()
    {
        await using var factory = await CreateSeededFactory();
        using var client = ClientFor(factory, AdminId);

        var page = await client.GetFromJsonAsync<PagedResultDto<AdminCarListItemDto>>("/api/admin/cars");

        Assert.Equal(4, page!.Total);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal([13, 12, 11, 10], page.Items.Select(i => i.Id));

        var car = page.Items.Single(i => i.Id == 10);
        Assert.Equal(SellerId, car.OwnerId);
        Assert.Equal(SellerId, car.UserId);
        Assert.Equal("Seller.One", car.OwnerName);
        Assert.Equal("seller@test.local", car.OwnerEmail);
        Assert.True(car.IsActive);
        Assert.NotNull(page.Items.Single(i => i.Id == 12).DeletedAt);
    }

    [Fact]
    public async Task Cars_SerializesExpectedFieldNames()
    {
        await using var factory = await CreateSeededFactory();
        using var client = ClientFor(factory, AdminId);

        var json = await client.GetStringAsync("/api/admin/cars?pageSize=1");

        foreach (var name in new[] { "total", "page", "pageSize", "items", "isActive", "ownerId", "ownerName", "ownerEmail", "userId", "deletedAt" })
            Assert.Contains($"\"{name}\":", json);
    }

    [Theory]
    [InlineData(true, new[] { 13, 10 })]
    [InlineData(false, new[] { 12, 11 })]
    public async Task Cars_ActiveFilter_StillWorks(bool active, int[] expected)
    {
        await using var factory = await CreateSeededFactory();
        using var client = ClientFor(factory, AdminId);

        var page = await client.GetFromJsonAsync<PagedResultDto<AdminCarListItemDto>>($"/api/admin/cars?active={active}");

        Assert.Equal(expected, page!.Items.Select(i => i.Id));
        Assert.Equal(expected.Length, page.Total);
    }

    [Fact]
    public async Task Cars_Pages()
    {
        await using var factory = await CreateSeededFactory();
        using var client = ClientFor(factory, AdminId);

        var page = await client.GetFromJsonAsync<PagedResultDto<AdminCarListItemDto>>("/api/admin/cars?page=2&pageSize=3");

        Assert.Equal([10], page!.Items.Select(i => i.Id));
        Assert.Equal(4, page.Total);
        Assert.Equal(2, page.Page);
        Assert.Equal(3, page.PageSize);
    }

    [Theory]
    [InlineData("pageSize=500", 1, 50, 50)]
    [InlineData("pageSize=0&page=0", 1, 20, 20)]
    [InlineData("pageSize=-5&page=-2", 1, 20, 20)]
    [InlineData("page=4&pageSize=20", 4, 20, 0)]   // past the end: empty, not an error
    public async Task Cars_PagingBounds(string query, int expectedPage, int expectedSize, int expectedCount)
    {
        await using var factory = await CreateSeededFactory(extraCars: 56);
        using var client = ClientFor(factory, AdminId);

        var page = await client.GetFromJsonAsync<PagedResultDto<AdminCarListItemDto>>($"/api/admin/cars?{query}");

        Assert.Equal(expectedPage, page!.Page);
        Assert.Equal(expectedSize, page.PageSize);
        Assert.Equal(expectedCount, page.Items.Count);
        Assert.Equal(60, page.Total);
    }

    // ---- users --------------------------------------------------------------

    [Fact]
    public async Task Users_ReturnsEnvelope_NewestFirst_WithCarCount()
    {
        await using var factory = await CreateSeededFactory();
        using var client = ClientFor(factory, AdminId);

        var page = await client.GetFromJsonAsync<PagedResultDto<AdminUserListItemDto>>("/api/admin/users");

        Assert.Equal(4, page!.Total);
        Assert.Equal(20, page.PageSize);
        Assert.Equal([QuietId, BlockedId, SellerId, AdminId], page.Items.Select(i => i.Id));

        var seller = page.Items.Single(i => i.Id == SellerId);
        Assert.Equal("seller@test.local", seller.Email);
        Assert.Equal("Seller.One", seller.Name);
        Assert.False(seller.IsBlocked);
        Assert.Equal(2, seller.CarCount);   // the deleted one is not counted
        Assert.Equal(0, page.Items.Single(i => i.Id == QuietId).CarCount);
    }

    [Fact]
    public async Task Users_SerializesExpectedFieldNames()
    {
        await using var factory = await CreateSeededFactory();
        using var client = ClientFor(factory, AdminId);

        var json = await client.GetStringAsync("/api/admin/users?pageSize=1");

        foreach (var name in new[] { "total", "page", "pageSize", "items", "id", "email", "name", "isBlocked", "createdAt", "carCount" })
            Assert.Contains($"\"{name}\":", json);
    }

    [Theory]
    [InlineData("SELLER", new[] { SellerId })]          // name, case-insensitive
    [InlineData("seller.one", new[] { SellerId })]
    [InlineData("BLOCKED@TEST", new[] { BlockedId })]   // email, case-insensitive
    [InlineData("  quiet  ", new[] { QuietId })]        // trimmed
    [InlineData("test.local", new[] { QuietId, BlockedId, SellerId, AdminId })]
    [InlineData("nobody", new int[0])]
    public async Task Users_SearchMatchesEmailOrName(string search, int[] expected)
    {
        await using var factory = await CreateSeededFactory();
        using var client = ClientFor(factory, AdminId);

        var page = await client.GetFromJsonAsync<PagedResultDto<AdminUserListItemDto>>(
            $"/api/admin/users?search={Uri.EscapeDataString(search)}");

        Assert.Equal(expected, page!.Items.Select(i => i.Id));
        Assert.Equal(expected.Length, page.Total);
    }

    [Theory]
    [InlineData(true, new[] { BlockedId })]
    [InlineData(false, new[] { QuietId, SellerId, AdminId })]
    public async Task Users_BlockedFilter(bool blocked, int[] expected)
    {
        await using var factory = await CreateSeededFactory();
        using var client = ClientFor(factory, AdminId);

        var page = await client.GetFromJsonAsync<PagedResultDto<AdminUserListItemDto>>($"/api/admin/users?blocked={blocked}");

        Assert.Equal(expected, page!.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Users_SearchAndBlockedCombine()
    {
        await using var factory = await CreateSeededFactory();
        using var client = ClientFor(factory, AdminId);

        var page = await client.GetFromJsonAsync<PagedResultDto<AdminUserListItemDto>>(
            "/api/admin/users?search=test.local&blocked=false&pageSize=2&page=2");

        Assert.Equal(3, page!.Total);
        Assert.Equal([AdminId], page.Items.Select(i => i.Id));
    }

    [Theory]
    [InlineData("pageSize=500", 1, 50, 50)]
    [InlineData("pageSize=0&page=0", 1, 20, 20)]
    [InlineData("page=2&pageSize=50", 2, 50, 10)]
    public async Task Users_PagingBounds(string query, int expectedPage, int expectedSize, int expectedCount)
    {
        await using var factory = await CreateSeededFactory(extraUsers: 56);
        using var client = ClientFor(factory, AdminId);

        var page = await client.GetFromJsonAsync<PagedResultDto<AdminUserListItemDto>>($"/api/admin/users?{query}");

        Assert.Equal(expectedPage, page!.Page);
        Assert.Equal(expectedSize, page.PageSize);
        Assert.Equal(expectedCount, page.Items.Count);
        Assert.Equal(60, page.Total);
    }

    // ---- setup --------------------------------------------------------------

    private static async Task<CarListingApiFactory> CreateSeededFactory(int extraCars = 0, int extraUsers = 0)
    {
        var factory = new CarListingApiFactory();
        var now = DateTime.UtcNow;
        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                NewUser(AdminId, "admin", "admin@test.local", "Admin", now.AddDays(-4)),
                NewUser(SellerId, "Seller.One", "seller@test.local", "User", now.AddDays(-3)),
                NewUser(BlockedId, "blocked.guy", "blocked@test.local", "User", now.AddDays(-2), blocked: true),
                NewUser(QuietId, "quiet", "quiet@test.local", "User", now.AddDays(-1)));

            // Older extras, so the four named users stay first.
            for (var i = 0; i < extraUsers; i++)
                db.Users.Add(NewUser(100 + i, $"extra{i}", $"extra{i}@example.org", "User", now.AddDays(-10 - i)));

            db.Cars.AddRange(
                NewCar(10, SellerId, isActive: true, now.AddHours(-4)),
                NewCar(11, SellerId, isActive: false, now.AddHours(-3)),
                NewCar(12, SellerId, isActive: false, now.AddHours(-2), deletedAt: now),
                NewCar(13, BlockedId, isActive: true, now.AddHours(-1)));

            for (var i = 0; i < extraCars; i++)
                db.Cars.Add(NewCar(1000 + i, SellerId, isActive: true, now.AddDays(-1 - i)));

            return Task.CompletedTask;
        });
        return factory;
    }

    private static HttpClient ClientFor(CarListingApiFactory factory, int userId)
    {
        var user = userId == AdminId
            ? NewUser(AdminId, "admin", "admin@test.local", "Admin", DateTime.UtcNow)
            : NewUser(userId, "Seller.One", "seller@test.local", "User", DateTime.UtcNow);

        using var scope = factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<TokenService>().CreateToken(user);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static User NewUser(int id, string name, string email, string role, DateTime createdAt, bool blocked = false) => new()
    {
        Id = id,
        Username = name,
        Email = email,
        PasswordHash = "hash",
        Role = role,
        IsBlocked = blocked,
        CreatedAt = createdAt
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
