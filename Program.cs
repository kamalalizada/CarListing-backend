using Entry.Data;
using Entry.Concrete;
using Entry.Services;
using Business.Concrete;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// DB
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT config
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var adminPassword = builder.Configuration["Admin:Password"];

if (string.IsNullOrWhiteSpace(jwtKey) || string.IsNullOrWhiteSpace(jwtIssuer))
    throw new Exception("Jwt config tapılmadı. appsettings.json-da Jwt:Key və Jwt:Issuer olmalıdır.");

if (IsPlaceholderSecret(jwtKey))
    throw new Exception("Jwt:Key placeholder ola bilməz. Real dəyəri Jwt__Key environment variable və ya user-secrets ilə ver.");

if (string.IsNullOrWhiteSpace(adminPassword))
    throw new Exception("Admin password tapılmadı. Admin:Password environment variable və ya development config ilə verilməlidir.");

// Services
builder.Services.AddScoped<Business.Concrete.TokenService>();
builder.Services.Configure<MinioOptions>(builder.Configuration.GetSection("Minio"));
builder.Services.AddScoped<IImageStorageService, MinioImageStorageService>();
builder.Services.AddScoped<LocalImageMigrationService>();

// CORS (Angular)
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Auth
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger + Bearer
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CarListing API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Authorize bölməsinə: Bearer {token} formatında yaz"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ✅ DB migrate + admin seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var admin = await db.Users.FirstOrDefaultAsync(u => u.Role == "Admin");
    if (admin is null)
    {
        admin = new User
        {
            Username = "admin",
            Email = "admin@local.test",
            Role = "Admin",
            IsBlocked = false,
            PasswordHash = PasswordService.Hash(adminPassword)
        };

        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }
    else if (!IsPbkdf2PasswordHash(admin.PasswordHash))
    {
        admin.PasswordHash = PasswordService.Hash(adminPassword);
        await db.SaveChangesAsync();
    }
}

if (args.Contains("--migrate-local-images", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var migration = scope.ServiceProvider.GetRequiredService<LocalImageMigrationService>();
    var migrated = await migration.MigrateAsync();
    Console.WriteLine($"Migrated {migrated} local car images to MinIO.");
    return;
}

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ✅ Static files (.avif/.webp mapping) — 404 problemini bu həll edir
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".webp"] = "image/webp";
provider.Mappings[".avif"] = "image/avif";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});

// CORS
app.UseCors("Frontend");

// Auth
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static bool IsPbkdf2PasswordHash(string passwordHash)
{
    if (string.IsNullOrWhiteSpace(passwordHash))
        return false;

    var parts = passwordHash.Split('.');
    return parts.Length == 3 && int.TryParse(parts[0], out _);
}

static bool IsPlaceholderSecret(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return true;

    return value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("SUPER_SECRET", StringComparison.OrdinalIgnoreCase);
}
