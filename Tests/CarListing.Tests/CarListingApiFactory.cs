using Entry.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CarListing.Tests;

public sealed class CarListingApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString("N");

    public CarListingApiFactory()
    {
        Environment.SetEnvironmentVariable("Startup__SkipTasks", "true");
        Environment.SetEnvironmentVariable("Jwt__Key", "strong-test-jwt-secret-at-least-32-chars");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "CarListingApi");
        Environment.SetEnvironmentVariable("Admin__Password", "strong-admin-password");
        Environment.SetEnvironmentVariable("Minio__Endpoint", "localhost:9000");
        Environment.SetEnvironmentVariable("Minio__AccessKey", "test-access-key");
        Environment.SetEnvironmentVariable("Minio__SecretKey", "test-secret-key");
        Environment.SetEnvironmentVariable("Minio__BucketName", "car-images");
        Environment.SetEnvironmentVariable("Minio__PublicBaseUrl", "http://localhost:9000/car-images");
        Environment.SetEnvironmentVariable("Minio__UseSsl", "false");
        Environment.SetEnvironmentVariable("Minio__MakeBucketPublic", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Startup:SkipTasks"] = "true",
                ["Jwt:Key"] = "strong-test-jwt-secret-at-least-32-chars",
                ["Jwt:Issuer"] = "CarListingApi",
                ["Admin:Password"] = "strong-admin-password",
                ["Minio:Endpoint"] = "localhost:9000",
                ["Minio:AccessKey"] = "test-access-key",
                ["Minio:SecretKey"] = "test-secret-key",
                ["Minio:BucketName"] = "car-images",
                ["Minio:PublicBaseUrl"] = "http://localhost:9000/car-images",
                ["Minio:UseSsl"] = "false",
                ["Minio:MakeBucketPublic"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    public async Task SeedAsync(Func<AppDbContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await seed(db);
        await db.SaveChangesAsync();
    }
}
