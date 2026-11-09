using System.Security.Cryptography;
using System.Text;
using Business.Concrete;
using Entry.Concrete;
using Entry.Data;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Entry.Services;

public enum ResetPasswordOutcome
{
    Success,
    InvalidToken,
}

public sealed class PasswordResetService
{
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);
    public const int MaxRequestsPerHour = 5;

    private readonly AppDbContext _db;
    private readonly IEmailQueue _emailQueue;
    private readonly FrontendOptions _frontend;
    private readonly TimeProvider _time;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        AppDbContext db,
        IEmailQueue emailQueue,
        IOptions<FrontendOptions> frontend,
        TimeProvider time,
        ILogger<PasswordResetService> logger)
    {
        _db = db;
        _emailQueue = emailQueue;
        _frontend = frontend.Value;
        _time = time;
        _logger = logger;
    }

    /// <summary>
    /// Creates a token and queues the email, if the account exists and is under
    /// the rate limit. Reports nothing either way: the caller answers 204 regardless.
    /// </summary>
    public async Task RequestAsync(string normalizedEmail, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        // A blocked user cannot log in anyway, so a new password would not help.
        if (user is null || user.IsBlocked)
            return;

        var baseUrl = _frontend.BaseUrl?.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
        {
            _logger.LogError("Frontend:BaseUrl is not configured; cannot build a password reset link.");
            return;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var windowStart = now - TimeSpan.FromHours(1);

        // Counted from the table itself, so the limit survives restarts and
        // holds across several API instances. Every accepted request creates
        // exactly one row, so rows per hour == requests per hour.
        var recent = await _db.PasswordResetTokens
            .CountAsync(t => t.UserId == user.Id && t.CreatedAt > windowStart, ct);
        if (recent >= MaxRequestsPerHour)
        {
            _logger.LogInformation("Password reset rate limit hit for user {UserId}.", user.Id);
            return;
        }

        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = Hash(token),
            CreatedAt = now,
            ExpiresAt = now + TokenLifetime,
        });
        await _db.SaveChangesAsync(ct);

        var link = $"{baseUrl}/reset-password" +
                   $"?email={Uri.EscapeDataString(user.Email)}" +
                   $"&token={Uri.EscapeDataString(token)}";

        var body =
            $"Salam, {user.Username}!\n\n" +
            "Şifrənizi bərpa etmək üçün aşağıdakı linkə keçin:\n\n" +
            $"{link}\n\n" +
            "Link 1 saat ərzində etibarlıdır və yalnız bir dəfə istifadə oluna bilər.\n" +
            "Bu sorğunu siz göndərməmisinizsə, bu məktubu nəzərə almayın.";

        if (!_emailQueue.TryEnqueue(new EmailMessage(user.Email, "Şifrənin bərpası", body)))
            _logger.LogError("Email queue is full; password reset email for user {UserId} dropped.", user.Id);
    }

    /// <summary>Sets the new password if the token is valid. The password must already be validated.</summary>
    public async Task<ResetPasswordOutcome> ResetAsync(
        string normalizedEmail,
        string token,
        string newPassword,
        CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
        if (user is null)
            return ResetPasswordOutcome.InvalidToken;

        var now = _time.GetUtcNow().UtcDateTime;
        var hash = Hash(token);

        var match = await _db.PasswordResetTokens.FirstOrDefaultAsync(t =>
            t.UserId == user.Id &&
            t.TokenHash == hash &&
            t.UsedAt == null &&
            t.ExpiresAt > now, ct);

        if (match is null)
            return ResetPasswordOutcome.InvalidToken;

        user.PasswordHash = PasswordService.Hash(newPassword);

        // Spend this token and every other outstanding one: after a reset, an
        // older link still sitting in the inbox must not work either.
        var outstanding = await _db.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null)
            .ToListAsync(ct);
        foreach (var t in outstanding)
            t.UsedAt = now;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request used a token in the meantime. UsedAt is a
            // concurrency token, so nothing from this request was saved.
            return ResetPasswordOutcome.InvalidToken;
        }

        return ResetPasswordOutcome.Success;
    }

    /// <summary>Lowercase hex SHA-256: what is stored instead of the token itself.</summary>
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
