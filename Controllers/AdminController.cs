using Entry.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Entry.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminController(AppDbContext db) => _db = db;

    [HttpPut("users/{id:int}/block")]
    public async Task<IActionResult> BlockUser(int id, [FromQuery] bool block = true)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null) return NotFound("User tapılmadı.");

        user.IsBlocked = block;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("cars/{id:int}/active")]
    public async Task<IActionResult> SetCarActive(int id, [FromQuery] bool active = true)
    {
        var car = await _db.Cars.FirstOrDefaultAsync(x => x.Id == id);
        if (car is null) return NotFound("Elan tapılmadı.");

        car.IsActive = active;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("cars")]
    public async Task<IActionResult> GetAllCars([FromQuery] bool? active = null)
    {
        var q = _db.Cars.AsNoTracking();

        if (active.HasValue)
            q = q.Where(x => x.IsActive == active.Value);

        var items = await q
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Brand,
                x.Model,
                x.Year,
                x.Price,
                x.UserId,
                x.IsActive,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(items);
    }
}
