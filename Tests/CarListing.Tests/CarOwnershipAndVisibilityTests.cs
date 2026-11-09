using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Business.Concrete;
using Entry.Concrete;
using Entry.Dto;
using Microsoft.Extensions.DependencyInjection;

namespace CarListing.Tests;

/// <summary>
/// End-to-end over HTTP with real JWTs, so the claim names, role mapping and
/// status codes are the ones the frontend actually sees.
/// </summary>
public class CarOwnershipAndVisibilityTests
{
    private const int OwnerId = 1;
    private const int OtherId = 2;
    private const int AdminId = 3;

    private const int ActiveCarId = 10;
    private const int InactiveCarId = 11;
    private const int NoImageCarId = 12;
    private const int NoMainImageCarId = 13;

    // ---- 1. mainImageUrl ----------------------------------------------------

    [Fact]
    public async Task List_ReturnsMainImageUrl_FallingBackToFirstByOrder_ThenNull()
    {
        await using var factory = await CreateSeededFactory();
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<PagedResultDto<CarListItemDto>>("/api/cars?pageSize=50");
        var byId = page!.Items.ToDictionary(i => i.Id);

        Assert.Equal("https://img.test/main.jpg", byId[ActiveCarId].MainImageUrl);
        Assert.Equal("https://img.test/first.jpg", byId[NoMainImageCarId].MainImageUrl);
        Assert.Null(byId[NoImageCarId].MainImageUrl);
    }

    [Fact]
    public async Task List_SerializesMainImageUrlInCamelCase()
    {
        await using var factory = await CreateSeededFactory();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/api/cars");

        Assert.Contains("\"mainImageUrl\":\"https://img.test/main.jpg\"", json);
        Assert.Contains("\"mainImageUrl\":null", json);
    }

    [Fact]
    public async Task Compare_ReturnsMainImageUrl()
    {
        await using var factory = await CreateSeededFactory();
        using var client = factory.CreateClient();

        var items = await client.GetFromJsonAsync<List<CarCompareItemDto>>(
            $"/api/cars/compare?ids={ActiveCarId}&ids={NoMainImageCarId}&ids={NoImageCarId}");

        Assert.Equal(
            new string?[] { "https://img.test/main.jpg", "https://img.test/first.jpg", null },
            items!.Select(i => i.MainImageUrl).ToArray());
    }

    // ---- 2. ownership -------------------------------------------------------

    public static TheoryData<string> CallerNames => new() { "other", "owner", "admin" };

    [Theory]
    [MemberData(nameof(CallerNames))]
    public async Task Update_Is403ForNonOwner_AndAllowedForOwnerAndAdmin(string caller)
    {
        await using var factory = await CreateSeededFactory();
        using var client = factory.CreateClient();
        Authorize(client, factory, caller);

        var response = await client.PutAsJsonAsync($"/api/cars/{ActiveCarId}", ValidUpdate());

        Assert.Equal(caller == "other" ? HttpStatusCode.Forbidden : HttpStatusCode.NoContent, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(CallerNames))]
    public async Task Delete_Is403ForNonOwner_AndAllowedForOwnerAndAdmin(string caller)
    {
        await using var factory = await CreateSeededFactory();
        using var client = factory.CreateClient();
        Authorize(client, factory, caller);

        var response = await client.DeleteAsync($"/api/cars/{ActiveCarId}");

        Assert.Equal(caller == "other" ? HttpStatusCode.Forbidden : HttpStatusCode.NoContent, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(CallerNames))]
    public async Task SetMainImage_Is403ForNonOwner_AndAllowedForOwnerAndAdmin(string caller)
    {
        await using var factory = await CreateSeededFactory();
        using var client = factory.CreateClient();
        Authorize(client, factory, caller);

        var response = await client.PutAsync($"/api/cars/{ActiveCarId}/images/102/main", null);

        Assert.Equal(caller == "other" ? HttpStatusCode.Forbidden : HttpStatusCode.NoContent, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(CallerNames))]
    public async Task ReorderImages_Is403ForNonOwner_AndAllowedForOwnerAndAdmin(string caller)
    {
        await using var factory = await CreateSeededFactory();
        using var client = factory.CreateClient();
        Authorize(client, factory, caller);

        var response = await client.PutAsJsonAsync(
            $"/api/cars/{ActiveCarId}/images/reorder",
            new ReorderImagesDto { ImageIds = [102, 101] });

        Assert.Equal(caller == "other" ? HttpStatusCode.Forbidden : HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteImage_Is403ForNonOwner()
    {
        await using var factory = await CreateSeededFactory();
        using var client = factory.CreateClient();
        Authorize(client, factory, "other");

        var response = await client.DeleteAsync($"/api/cars/{ActiveCarId}/images/101");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadImages_Is403ForNonOwner()
    {
        await using var factory = await CreateSeededFactory();
        using var client = factory.CreateClient();
        Authorize(client, factory, "other");

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "files", "photo.jpg");

        var response = await client.PostAsync($"/api/cars/{ActiveCarId}/images", form);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Mutations_Are401WithoutToken()
    {
        await using var factory = await CreateSeededFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/cars/{ActiveCarId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- 3. sub claim -------------------------------------------------------

    [Fact]
    public async Task Token_SubClaim_EqualsCarUserId()
    {
        await using var factory = new CarListingApiFactory();
        using var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterDto
        {
            Username = "seller",
            Email = "seller@test.local",
            Password = "secret123"
        });
        register.EnsureSuccessStatusCode();
        var token = (await register.Content.ReadFromJsonAsync<AuthResponseDto>())!.Token;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var create = await client.PostAsJsonAsync("/api/cars", new CreateCarDto
        {
            Title = "Toyota Camry",
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
            ContactPhone = "+994501234567"
        });
        create.EnsureSuccessStatusCode();

        var car = await client.GetFromJsonAsync<CarDetailsDto>(create.Headers.Location);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var sub = Assert.Single(jwt.Claims, c => c.Type == "sub").Value;

        Assert.Equal(car!.UserId.ToString(), sub);
        // The claim the token carried before "sub" was added is still there,
        // under its full URI, with the same value.
        Assert.Equal(sub, Assert.Single(jwt.Claims, c => c.Type == ClaimTypes.NameIdentifier).Value);
    }

    // ---- 4. inactive cars ---------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("other")]
    public async Task GetById_InactiveCar_Is404ForPublicAndNonOwner(string? caller)
    {
        await using var factory = await CreateSeededFactory();
        using var client = factory.CreateClient();
        if (caller is not null) Authorize(client, factory, caller);

        var response = await client.GetAsync($"/api/cars/{InactiveCarId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("admin")]
    public async Task GetById_InactiveCar_IsVisibleToOwnerAndAdmin_WithIsActiveFalse(string caller)
    {
        await using var factory = await CreateSeededFactory();
        using var client = factory.CreateClient();
        Authorize(client, factory, caller);

        var response = await client.GetAsync($"/api/cars/{InactiveCarId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"isActive\":false", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetById_ActiveCar_IsPublic_WithIsActiveTrue()
    {
        await using var factory = await CreateSeededFactory();
        using var client = factory.CreateClient();

        var car = await client.GetFromJsonAsync<CarDetailsDto>($"/api/cars/{ActiveCarId}");

        Assert.True(car!.IsActive);
    }

    [Fact]
    public async Task ListAndCompare_ExcludeInactiveCars_EvenForOwner()
    {
        await using var factory = await CreateSeededFactory();
        using var client = factory.CreateClient();
        Authorize(client, factory, "owner");

        var page = await client.GetFromJsonAsync<PagedResultDto<CarListItemDto>>("/api/cars?pageSize=50");
        var compare = await client.GetAsync($"/api/cars/compare?ids={ActiveCarId}&ids={InactiveCarId}");

        Assert.DoesNotContain(page!.Items, i => i.Id == InactiveCarId);
        Assert.Equal(3, page.Total);
        Assert.Equal(HttpStatusCode.BadRequest, compare.StatusCode);
    }

    // ---- setup --------------------------------------------------------------

    private static async Task<CarListingApiFactory> CreateSeededFactory()
    {
        var factory = new CarListingApiFactory();
        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                NewUser(OwnerId, "owner", "User"),
                NewUser(OtherId, "other", "User"),
                NewUser(AdminId, "admin", "Admin"));

            db.Cars.AddRange(
                NewCar(ActiveCarId, isActive: true),
                NewCar(InactiveCarId, isActive: false),
                NewCar(NoImageCarId, isActive: true),
                NewCar(NoMainImageCarId, isActive: true));

            db.CarImages.AddRange(
                // Main is deliberately not first by order.
                new CarImage { Id = 101, CarId = ActiveCarId, ImageUrl = "https://img.test/first-but-not-main.jpg", Order = 0 },
                new CarImage { Id = 102, CarId = ActiveCarId, ImageUrl = "https://img.test/main.jpg", Order = 1, IsMain = true },
                // No main: the lowest order wins, regardless of insert order.
                new CarImage { Id = 103, CarId = NoMainImageCarId, ImageUrl = "https://img.test/second.jpg", Order = 1 },
                new CarImage { Id = 104, CarId = NoMainImageCarId, ImageUrl = "https://img.test/first.jpg", Order = 0 });

            return Task.CompletedTask;
        });
        return factory;
    }

    private static void Authorize(HttpClient client, CarListingApiFactory factory, string caller)
    {
        var user = caller switch
        {
            "owner" => NewUser(OwnerId, "owner", "User"),
            "other" => NewUser(OtherId, "other", "User"),
            "admin" => NewUser(AdminId, "admin", "Admin"),
            _ => throw new ArgumentOutOfRangeException(nameof(caller))
        };

        using var scope = factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<TokenService>().CreateToken(user);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static User NewUser(int id, string name, string role) => new()
    {
        Id = id,
        Username = name,
        Email = $"{name}@test.local",
        PasswordHash = "hash",
        Role = role
    };

    private static Car NewCar(int id, bool isActive) => new()
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
        UserId = OwnerId,
        IsActive = isActive,
        CreatedAt = DateTime.UtcNow.AddMinutes(-id)
    };

    private static UpdateCarDto ValidUpdate() => new()
    {
        Title = "Updated",
        Brand = "Toyota",
        Model = "Camry",
        Year = 2021,
        Price = 16_000,
        Mileage = 70_000,
        FuelType = "Petrol",
        BodyType = "Sedan",
        Transmission = "Automatic",
        Engine = "2.0",
        City = "Baku",
        ContactPhone = "+994501234567"
    };
}
