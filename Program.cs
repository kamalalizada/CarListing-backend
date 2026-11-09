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
builder.Services.AddScoped<MinioImageStorageService>();
builder.Services.AddScoped<IImageStorageService>(sp => sp.GetRequiredService<MinioImageStorageService>());
builder.Services.AddScoped<IImageStorageProvisioner>(sp => sp.GetRequiredService<MinioImageStorageService>());
builder.Services.AddScoped<LocalImageMigrationService>();
builder.Services.AddScoped<InactiveCarImageCleanupService>();
builder.Services.AddScoped<BlockedUserTokenValidator>();

// Password reset email. Without an SMTP host the link is logged instead (full
// link in Development only), so the flow works without a mail server.
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<FrontendOptions>(builder.Configuration.GetSection(FrontendOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<PasswordResetService>();
builder.Services.AddSingleton<ChannelEmailQueue>();
builder.Services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<ChannelEmailQueue>());
builder.Services.AddHostedService<EmailDispatchService>();
if (builder.Configuration.GetSection(SmtpOptions.SectionName).Get<SmtpOptions>()?.IsConfigured == true)
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
else
    builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();

// AI search. The rule half always runs - it is free, instant, and better than
// the parser it replaced. The model is added only when a key exists, because a
// missing key is a deployment choice (local dev, CI), not a runtime error.
builder.Services.Configure<AiSearchOptions>(
    builder.Configuration.GetSection(AiSearchOptions.SectionName));
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ISearchVocabularyProvider, DbSearchVocabularyProvider>();
builder.Services.AddScoped<IAiSearchParserService, HybridAiSearchParserService>();

var aiSearchEnabled = builder.Configuration.GetValue(
    $"{AiSearchOptions.SectionName}:Enabled", true);
var ollamaBaseUrl = builder.Configuration[
    $"{AiSearchOptions.SectionName}:OllamaBaseUrl"];
var geminiApiKey = builder.Configuration["GEMINI_API_KEY"];

// Whichever model is configured is registered behind IModelSearchParser, so
// the hybrid escalates to "a model" rather than to a particular vendor. Ollama
// wins when both are present: it is local, free and unmetered, and it is only
// ever configured on a developer's own machine.
if (aiSearchEnabled && !string.IsNullOrWhiteSpace(ollamaBaseUrl))
{
    builder.Services.AddHttpClient<IModelSearchParser, OllamaAiSearchParserService>(http =>
    {
        http.BaseAddress = new Uri(ollamaBaseUrl);

        // A 7B model on CPU is slow, but a buyer is watching a spinner. Past
        // this the rule parser answers instead.
        http.Timeout = TimeSpan.FromSeconds(
            builder.Configuration.GetValue($"{AiSearchOptions.SectionName}:TimeoutSeconds", 10));
    });
}
else if (aiSearchEnabled && !string.IsNullOrWhiteSpace(geminiApiKey))
{
    // One client per process: it owns an HttpClient, and a new one per request
    // would exhaust sockets under load.
    builder.Services.AddSingleton(new Google.GenAI.Client(apiKey: geminiApiKey));
    builder.Services.AddScoped<IModelSearchParser, GeminiAiSearchParserService>();
}

// CORS (local frontend development servers)
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:4200",
                "http://localhost:5173")
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
        opt.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var validator = context.HttpContext.RequestServices.GetRequiredService<BlockedUserTokenValidator>();
                if (context.Principal is not null &&
                    await validator.IsBlockedAsync(context.Principal, context.HttpContext.RequestAborted))
                {
                    context.Fail("User is blocked.");
                }
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger + Bearer
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CarListing API", Version = "v1" });

    // Endpoint summaries and response descriptions from the controllers' XML comments.
    var xmlDocs = Path.Combine(AppContext.BaseDirectory, "wCarListing.xml");
    if (File.Exists(xmlDocs))
        c.IncludeXmlComments(xmlDocs);

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
var skipStartupTasks = builder.Configuration.GetValue<bool>("Startup:SkipTasks");

// ✅ DB migrate + admin seed
if (!skipStartupTasks)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var imageStorageProvisioner = scope.ServiceProvider.GetRequiredService<IImageStorageProvisioner>();

    await db.Database.MigrateAsync();
    await imageStorageProvisioner.ProvisionAsync();

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

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

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

public partial class Program;
