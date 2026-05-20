using Business.Concrete;

namespace CarListing.Tests;

public class PasswordServiceTests
{
    [Fact]
    public void Hash_CanBeVerified_WithOriginalPassword()
    {
        var hash = PasswordService.Hash("StrongPassword123!");

        Assert.True(PasswordService.Verify("StrongPassword123!", hash));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForDifferentPassword()
    {
        var hash = PasswordService.Hash("StrongPassword123!");

        Assert.False(PasswordService.Verify("wrong-password", hash));
    }
}
