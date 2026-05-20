using System.Security.Claims;
using Entry.Concrete;
using Entry.Data;
using Entry.Services;
using Microsoft.EntityFrameworkCore;

namespace CarListing.Tests;

public class BlockedUserTokenValidatorTests
{
    [Fact]
    public async Task IsBlockedAsync_ReturnsTrue_ForBlockedUser()
    {
        await using var db = CreateDbContext();
        await SeedUser(db, isBlocked: true);
        var validator = new BlockedUserTokenValidator(db);

        var isBlocked = await validator.IsBlockedAsync(CreatePrincipal(userId: 1));

        Assert.True(isBlocked);
    }

    [Fact]
    public async Task IsBlockedAsync_ReturnsFalse_ForActiveUser()
    {
        await using var db = CreateDbContext();
        await SeedUser(db, isBlocked: false);
        var validator = new BlockedUserTokenValidator(db);

        var isBlocked = await validator.IsBlockedAsync(CreatePrincipal(userId: 1));

        Assert.False(isBlocked);
    }

    [Fact]
    public async Task IsBlockedAsync_ReturnsFalse_WhenClaimIsMissing()
    {
        await using var db = CreateDbContext();
        await SeedUser(db, isBlocked: true);
        var validator = new BlockedUserTokenValidator(db);

        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var isBlocked = await validator.IsBlockedAsync(principal);

        Assert.False(isBlocked);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static async Task SeedUser(AppDbContext db, bool isBlocked)
    {
        db.Users.Add(new User
        {
            Id = 1,
            Username = "user",
            Email = "user@test.local",
            PasswordHash = "hash",
            Role = "User",
            IsBlocked = isBlocked
        });

        await db.SaveChangesAsync();
    }

    private static ClaimsPrincipal CreatePrincipal(int userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ], "TestAuth"));
    }
}
