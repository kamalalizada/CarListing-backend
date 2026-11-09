using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Business.Concrete;
using Entry.Concrete;
using Entry.Controllers;
using Entry.Data;
using Entry.Dto;
using Entry.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CarListing.Tests;

public class PasswordResetTests
{
    private const string Email = "seller@test.local";
    private const string OldPassword = "oldpass1";
    private const string NewPassword = "newpass1";

    // ---- forgot-password ----------------------------------------------------

    [Fact]
    public async Task Forgot_UnknownEmail_Is204_AndSendsNothing()
    {
        await using var app = await TestApp.CreateAsync();

        var response = await app.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "nobody@test.local" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(app.Emails.Messages);
    }

    [Fact]
    public async Task Forgot_KnownEmail_Is204_AndEmailsAUrlEncodedLink()
    {
        await using var app = await TestApp.CreateAsync();

        // Same normalization as register: trimmed, lowercased.
        var response = await app.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "  Seller@Test.LOCAL " });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var message = Assert.Single(app.Emails.Messages);
        Assert.Equal(Email, message.To);
        Assert.Matches(@"http://localhost:5173/reset-password\?email=seller%40test\.local&token=[A-Za-z0-9_-]{43}\b", message.Body);
    }

    [Fact]
    public async Task Forgot_StoresOnlyAHashOfTheToken_ValidForOneHour()
    {
        await using var app = await TestApp.CreateAsync();

        await app.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email });

        var token = TokenFrom(Assert.Single(app.Emails.Messages));
        var row = Assert.Single(await app.Db(db => db.PasswordResetTokens.AsNoTracking().ToListAsync()));
        Assert.NotEqual(token, row.TokenHash);
        Assert.Equal(PasswordResetService.Hash(token), row.TokenHash);
        Assert.Equal(TimeSpan.FromHours(1), row.ExpiresAt - row.CreatedAt);
        Assert.Null(row.UsedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task Forgot_MissingOrMalformedEmail_Is400WithMessage(string? email)
    {
        await using var app = await TestApp.CreateAsync();

        var response = await app.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AuthController.InvalidEmailMessage, (await ReadMessage(response)));
    }

    [Fact]
    public async Task Forgot_RateLimit_FivePerHour_StillAnswers204()
    {
        await using var app = await TestApp.CreateAsync();

        for (var i = 0; i < 7; i++)
        {
            var response = await app.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email });
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.Equal(5, app.Emails.Messages.Count);

        // The window slides: an hour later the address can ask again.
        app.Clock.Advance(TimeSpan.FromMinutes(61));
        await app.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email });
        Assert.Equal(6, app.Emails.Messages.Count);
    }

    [Fact]
    public async Task Forgot_BlockedUser_SendsNothing()
    {
        await using var app = await TestApp.CreateAsync(blocked: true);

        var response = await app.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(app.Emails.Messages);
    }

    // ---- reset-password -----------------------------------------------------

    [Fact]
    public async Task Reset_ChangesPassword_AndTokenIsSingleUse()
    {
        await using var app = await TestApp.CreateAsync();
        var token = await app.RequestTokenAsync();

        var reset = await app.Client.PostAsJsonAsync("/api/auth/reset-password",
            new { email = Email, token, newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(app, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(app, OldPassword)).StatusCode);

        var again = await app.Client.PostAsJsonAsync("/api/auth/reset-password",
            new { email = Email, token, newPassword = "another1" });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal(AuthController.InvalidResetLinkMessage, await ReadMessage(again));
    }

    [Fact]
    public async Task Reset_AlsoInvalidatesOlderOutstandingLinks()
    {
        await using var app = await TestApp.CreateAsync();
        var first = await app.RequestTokenAsync();
        var second = await app.RequestTokenAsync();

        await app.Client.PostAsJsonAsync("/api/auth/reset-password", new { email = Email, token = second, newPassword = NewPassword });
        var response = await app.Client.PostAsJsonAsync("/api/auth/reset-password",
            new { email = Email, token = first, newPassword = "another1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reset_ExpiredToken_Is400()
    {
        await using var app = await TestApp.CreateAsync();
        var token = await app.RequestTokenAsync();
        app.Clock.Advance(TimeSpan.FromMinutes(61));

        var response = await app.Client.PostAsJsonAsync("/api/auth/reset-password",
            new { email = Email, token, newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AuthController.InvalidResetLinkMessage, await ReadMessage(response));
        Assert.Equal(HttpStatusCode.OK, (await Login(app, OldPassword)).StatusCode);
    }

    [Theory]
    [InlineData("other@test.local", "valid")]   // token belongs to another account
    [InlineData(Email, "made-up-token")]
    [InlineData(Email, "")]
    [InlineData("", "valid")]
    public async Task Reset_WrongEmailOrToken_Is400(string email, string tokenKind)
    {
        await using var app = await TestApp.CreateAsync();
        var real = await app.RequestTokenAsync();
        var token = tokenKind == "valid" ? real : tokenKind;

        var response = await app.Client.PostAsJsonAsync("/api/auth/reset-password",
            new { email, token, newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AuthController.InvalidResetLinkMessage, await ReadMessage(response));
    }

    [Theory]
    [InlineData("12345", PasswordRules.LengthMessage)]
    [InlineData(null, PasswordRules.RequiredMessage)]
    [InlineData("   ", PasswordRules.RequiredMessage)]
    public async Task Reset_BadPassword_Is400WithRegisterMessage_AndKeepsTheToken(string? newPassword, string expected)
    {
        await using var app = await TestApp.CreateAsync();
        var token = await app.RequestTokenAsync();

        var response = await app.Client.PostAsJsonAsync("/api/auth/reset-password",
            new { email = Email, token, newPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expected, await ReadMessage(response));

        // A typo in the password must not burn the link.
        var retry = await app.Client.PostAsJsonAsync("/api/auth/reset-password",
            new { email = Email, token, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
    }

    [Fact]
    public async Task Register_UsesTheSamePasswordMessages()
    {
        await using var app = await TestApp.CreateAsync();

        var response = await app.Client.PostAsJsonAsync("/api/auth/register",
            new { username = "newbie", email = "newbie@test.local", password = "12345" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = json.RootElement.GetProperty("errors").GetProperty("Password");
        Assert.Equal(PasswordRules.LengthMessage, Assert.Single(errors.EnumerateArray()).GetString());
    }

    [Theory]
    [InlineData("123456", true)]
    [InlineData("12345", false)]
    [InlineData("      ", false)]
    public void PasswordRules_LengthIs6To100(string password, bool valid)
    {
        Assert.Equal(valid, PasswordRules.Validate(password) is null);
        Assert.Null(PasswordRules.Validate(new string('a', 100)));
        Assert.Equal(PasswordRules.LengthMessage, PasswordRules.Validate(new string('a', 101)));
    }

    // ---- delivery -----------------------------------------------------------

    [Fact]
    public async Task Forgot_RealQueue_DeliversThroughTheBackgroundSender()
    {
        var sender = new CapturingEmailSender();
        await using var factory = new CarListingApiFactory
        {
            OverrideServices = s =>
            {
                s.Configure<FrontendOptions>(o => o.BaseUrl = "http://localhost:5173");
                s.RemoveAll<IEmailSender>();
                s.AddSingleton<IEmailSender>(sender);
            }
        };
        await SeedUser(factory);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email });

        var delivered = await sender.First.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains("/reset-password?email=", delivered.Body);
    }

    [Fact]
    public async Task LoggingSender_InDevelopment_LogsTheFullLink()
    {
        var logger = new ListLogger<LoggingEmailSender>();
        var sender = new LoggingEmailSender(new FakeEnvironment(Environments.Development), logger);

        await sender.SendAsync(new EmailMessage(Email, "Şifrənin bərpası", "link: http://x/reset-password?token=abc"));

        Assert.Contains(logger.Lines, l => l.Contains("http://x/reset-password?token=abc"));
    }

    [Fact]
    public async Task LoggingSender_OutsideDevelopment_NeverLogsTheLink()
    {
        var logger = new ListLogger<LoggingEmailSender>();
        var sender = new LoggingEmailSender(new FakeEnvironment(Environments.Production), logger);

        await sender.SendAsync(new EmailMessage(Email, "Şifrənin bərpası", "link: http://x/reset-password?token=abc"));

        Assert.NotEmpty(logger.Lines);
        Assert.DoesNotContain(logger.Lines, l => l.Contains("token=abc"));
    }

    // ---- helpers ------------------------------------------------------------

    private static Task<HttpResponseMessage> Login(TestApp app, string password) =>
        app.Client.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = Email, Password = password });

    private static async Task<string?> ReadMessage(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<MessageResponseDto>())?.Message;

    private static string TokenFrom(EmailMessage message) =>
        Uri.UnescapeDataString(Regex.Match(message.Body, @"[?&]token=([^\s&]+)").Groups[1].Value);

    private static Task SeedUser(CarListingApiFactory factory, bool blocked = false) =>
        factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                new User
                {
                    Id = 1,
                    Username = "seller",
                    Email = Email,
                    PasswordHash = PasswordService.Hash(OldPassword),
                    Role = "User",
                    IsBlocked = blocked
                },
                new User
                {
                    Id = 2,
                    Username = "other",
                    Email = "other@test.local",
                    PasswordHash = PasswordService.Hash(OldPassword),
                    Role = "User"
                });
            return Task.CompletedTask;
        });

    /// <summary>An API with a recording email queue and a controllable clock.</summary>
    private sealed class TestApp : IAsyncDisposable
    {
        public required CarListingApiFactory Factory { get; init; }
        public required HttpClient Client { get; init; }
        public required RecordingEmailQueue Emails { get; init; }
        public required ManualClock Clock { get; init; }

        public static async Task<TestApp> CreateAsync(bool blocked = false)
        {
            var emails = new RecordingEmailQueue();
            var clock = new ManualClock();
            var factory = new CarListingApiFactory
            {
                OverrideServices = s =>
                {
                    s.Configure<FrontendOptions>(o => o.BaseUrl = "http://localhost:5173/");
                    s.RemoveAll<IEmailQueue>();
                    s.AddSingleton<IEmailQueue>(emails);
                    s.RemoveAll<TimeProvider>();
                    s.AddSingleton<TimeProvider>(clock);
                }
            };
            await SeedUser(factory, blocked);
            return new TestApp { Factory = factory, Client = factory.CreateClient(), Emails = emails, Clock = clock };
        }

        public async Task<string> RequestTokenAsync()
        {
            var before = Emails.Messages.Count;
            var response = await Client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email });
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(before + 1, Emails.Messages.Count);
            return TokenFrom(Emails.Messages[^1]);
        }

        public async Task<T> Db<T>(Func<AppDbContext, Task<T>> query)
        {
            using var scope = Factory.Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();
        }
    }

    private sealed class RecordingEmailQueue : IEmailQueue
    {
        private readonly List<EmailMessage> _messages = [];

        public IReadOnlyList<EmailMessage> Messages
        {
            get { lock (_messages) return _messages.ToList(); }
        }

        public bool TryEnqueue(EmailMessage message)
        {
            lock (_messages) _messages.Add(message);
            return true;
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public TaskCompletionSource<EmailMessage> First { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            First.TrySetResult(message);
            return Task.CompletedTask;
        }
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Lines.Add(formatter(state, exception));
    }

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "CarListing";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
