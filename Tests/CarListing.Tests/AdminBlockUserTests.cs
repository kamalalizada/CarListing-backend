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

/// <summary>PUT /api/admin/users/{id}/block, over HTTP.</summary>
public class AdminBlockUserTests
{
    private const int AdminId = 1;
    private const int UserId = 2;

    [Fact]
    public async Task BlockingYourself_Is400WithMessage_AndChangesNothing()
    {
        await using var factory = await CreateSeededFactory();
        using var client = AdminClient(factory);

        var response = await client.PutAsync($"/api/admin/users/{AdminId}/block?block=true", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MessageResponseDto>();
        Assert.Equal("Öz hesabınızı bloklaya bilməzsiniz.", body!.Message);
        Assert.False(await IsBlocked(factory, AdminId));
    }

    [Fact]
    public async Task BlockingYourself_WithDefaultBlockParameter_IsAlso400()
    {
        // block defaults to true, so leaving it out is still a block.
        await using var factory = await CreateSeededFactory();
        using var client = AdminClient(factory);

        var response = await client.PutAsync($"/api/admin/users/{AdminId}/block", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await IsBlocked(factory, AdminId));
    }

    [Fact]
    public async Task UnblockingYourself_IsAllowed()
    {
        await using var factory = await CreateSeededFactory();
        using var client = AdminClient(factory);

        var response = await client.PutAsync($"/api/admin/users/{AdminId}/block?block=false", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await IsBlocked(factory, AdminId));
    }

    [Fact]
    public async Task BlockingSomeoneElse_StillWorks()
    {
        await using var factory = await CreateSeededFactory();
        using var client = AdminClient(factory);

        var response = await client.PutAsync($"/api/admin/users/{UserId}/block?block=true", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(await IsBlocked(factory, UserId));
    }

    [Fact]
    public void Message_MatchesTheConstant()
    {
        Assert.Equal("Öz hesabınızı bloklaya bilməzsiniz.", AdminController.CannotBlockSelfMessage);
    }

    private static async Task<bool> IsBlocked(CarListingApiFactory factory, int id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Users.AsNoTracking().SingleAsync(u => u.Id == id)).IsBlocked;
    }

    private static async Task<CarListingApiFactory> CreateSeededFactory()
    {
        var factory = new CarListingApiFactory();
        await factory.SeedAsync(db =>
        {
            db.Users.AddRange(NewUser(AdminId, "admin", "Admin"), NewUser(UserId, "seller", "User"));
            return Task.CompletedTask;
        });
        return factory;
    }

    private static HttpClient AdminClient(CarListingApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<TokenService>().CreateToken(NewUser(AdminId, "admin", "Admin"));

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
}
