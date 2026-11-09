using Business.Concrete;
using Entry.Concrete;
using Entry.Data;
using Entry.Dto;
using Entry.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TokenService = Business.Concrete.TokenService;

namespace Entry.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokenService;
    private readonly PasswordResetService _passwordReset;

    public const string InvalidResetLinkMessage = "Bərpa linki etibarsızdır və ya vaxtı keçib.";
    public const string InvalidEmailMessage = "Email düzgün formatda deyil.";

    public AuthController(AppDbContext db, TokenService tokenService, PasswordResetService passwordReset)
    {
        _db = db;
        _tokenService = tokenService;
        _passwordReset = passwordReset;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        dto.Username = dto.Username.Trim();
        dto.Email = dto.Email.Trim().ToLowerInvariant();

        var exists = await _db.Users.AnyAsync(u => u.Email == dto.Email || u.Username == dto.Username);
        if (exists) return BadRequest("Bu email və ya username artıq mövcuddur.");

        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = PasswordService.Hash(dto.Password),
            Role = "User",
            IsBlocked = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _tokenService.CreateToken(user);
        return Ok(new AuthResponseDto { Token = token });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null) return Unauthorized("Email və ya şifrə yanlışdır.");

        if (user.IsBlocked) return Unauthorized("User bloklanıb.");

        var ok = PasswordService.Verify(dto.Password, user.PasswordHash);
        if (!ok) return Unauthorized("Email və ya şifrə yanlışdır.");

        var token = _tokenService.CreateToken(user);
        return Ok(new AuthResponseDto { Token = token });
    }

    /// <summary>
    /// Emails a password reset link, if the address has an account.
    /// </summary>
    /// <remarks>
    /// Always 204 for a well-formed email, whether or not an account exists,
    /// so the endpoint cannot be used to find out who is registered. The link
    /// is {Frontend:BaseUrl}/reset-password?email=...&amp;token=..., valid for
    /// 1 hour and usable once. At most 5 requests per email per hour; beyond
    /// that the answer is still 204 but nothing is sent.
    /// </remarks>
    /// <response code="204">Accepted. An email is sent only if the account exists and is under the limit.</response>
    /// <response code="400">Email missing or malformed: { message }.</response>
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto, CancellationToken ct)
    {
        var email = NormalizeEmail(dto.Email);
        if (email is null)
            return BadRequest(new MessageResponseDto(InvalidEmailMessage));

        await _passwordReset.RequestAsync(email, ct);
        return NoContent();
    }

    /// <summary>
    /// Sets a new password using the token from the reset email.
    /// </summary>
    /// <remarks>
    /// The password follows the same rules as register: required, 6-100
    /// characters. On success the token, and any other unused reset token of
    /// the account, stops working.
    /// </remarks>
    /// <response code="204">Password changed.</response>
    /// <response code="400">Invalid, expired or used link, or the password breaks the rules: { message }.</response>
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto, CancellationToken ct)
    {
        var email = NormalizeEmail(dto.Email);
        if (email is null || string.IsNullOrWhiteSpace(dto.Token))
            return BadRequest(new MessageResponseDto(InvalidResetLinkMessage));

        var passwordError = PasswordRules.Validate(dto.NewPassword);
        if (passwordError is not null)
            return BadRequest(new MessageResponseDto(passwordError));

        var outcome = await _passwordReset.ResetAsync(email, dto.Token.Trim(), dto.NewPassword!, ct);

        return outcome == ResetPasswordOutcome.Success
            ? NoContent()
            : BadRequest(new MessageResponseDto(InvalidResetLinkMessage));
    }

    /// <summary>Trimmed and lowercased, the way register stores it; null if not an email.</summary>
    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;

        var trimmed = email.Trim();
        return new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(trimmed) && trimmed.Length <= 100
            ? trimmed.ToLowerInvariant()
            : null;
    }
}
