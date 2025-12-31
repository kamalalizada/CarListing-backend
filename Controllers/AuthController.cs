using Business.Concrete;
using Entry.Concrete;
using Entry.Data;
using Entry.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.WSIdentity;
using TokenService = Business.Concrete.TokenService;

namespace Entry.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokenService;

    public AuthController(AppDbContext db, TokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        dto.Username = dto.Username.Trim();
        dto.Email = dto.Email.Trim().ToLower();

        if (string.IsNullOrWhiteSpace(dto.Username) ||
            string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest("Username, Email, Password boş ola bilməz.");

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
        var email = dto.Email.Trim().ToLower();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null) return Unauthorized("Email və ya şifrə yanlışdır.");

        if (user.IsBlocked) return Unauthorized("User bloklanıb.");

        var ok = PasswordService.Verify(dto.Password, user.PasswordHash);
        if (!ok) return Unauthorized("Email və ya şifrə yanlışdır.");

        var token = _tokenService.CreateToken(user);
        return Ok(new AuthResponseDto { Token = token });
    }
}
