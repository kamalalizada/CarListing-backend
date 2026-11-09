using Entry.Data;
using Entry.Dto;
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

    /// <summary>Deactivates or re-activates a listing. Deleted listings are 404: this never undeletes.</summary>
    [HttpPut("cars/{id:int}/active")]
    public async Task<IActionResult> SetCarActive(int id, [FromQuery] bool active = true)
    {
        var car = await _db.Cars.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (car is null) return NotFound("Elan tapılmadı.");

        car.IsActive = active;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>All listings, newest first, deleted ones included.</summary>
    /// <param name="active">Optional: true for public listings, false for deactivated or deleted ones.</param>
    /// <param name="page">1-based page. Default 1.</param>
    /// <param name="pageSize">Default 20, at most 50 (larger values are capped).</param>
    /// <response code="200">{ total, page, pageSize, items }.</response>
    /// <response code="401">No or invalid token.</response>
    /// <response code="403">Not an admin.</response>
    [HttpGet("cars")]
    [ProducesResponseType(typeof(PagedResultDto<AdminCarListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResultDto<AdminCarListItemDto>>> GetAllCars(
        [FromQuery] bool? active = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        CancellationToken ct = default)
    {
        var (currentPage, size) = NormalizePaging(page, pageSize);

        var q = _db.Cars.AsNoTracking();

        if (active.HasValue)
            q = q.Where(x => x.IsActive == active.Value);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((currentPage - 1) * size)
            .Take(size)
            .Select(x => new AdminCarListItemDto
            {
                Id = x.Id,
                Title = x.Title,
                Brand = x.Brand,
                Model = x.Model,
                Year = x.Year,
                Price = x.Price,
                Mileage = x.Mileage,
                FuelType = x.FuelType,
                BodyType = x.BodyType,
                Transmission = x.Transmission,
                Engine = x.Engine,
                City = x.City,
                Color = x.Color,
                UserId = x.UserId,
                IsActive = x.IsActive,
                DeletedAt = x.DeletedAt,
                CreatedAt = x.CreatedAt,
                OwnerId = x.UserId,
                OwnerName = x.User.Username,
                OwnerEmail = x.User.Email,
            })
            .ToListAsync(ct);

        return Ok(new PagedResultDto<AdminCarListItemDto>
        {
            Total = total,
            Page = currentPage,
            PageSize = size,
            Items = items
        });
    }

    /// <summary>All users, newest first.</summary>
    /// <param name="search">Optional: part of the email or username, case-insensitive.</param>
    /// <param name="blocked">Optional: true for blocked users only, false for unblocked only.</param>
    /// <param name="page">1-based page. Default 1.</param>
    /// <param name="pageSize">Default 20, at most 50 (larger values are capped).</param>
    /// <response code="200">{ total, page, pageSize, items }. carCount excludes deleted listings.</response>
    /// <response code="401">No or invalid token.</response>
    /// <response code="403">Not an admin.</response>
    [HttpGet("users")]
    [ProducesResponseType(typeof(PagedResultDto<AdminUserListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResultDto<AdminUserListItemDto>>> GetUsers(
        [FromQuery] string? search = null,
        [FromQuery] bool? blocked = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        CancellationToken ct = default)
    {
        var (currentPage, size) = NormalizePaging(page, pageSize);

        var q = _db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // ToLower on both sides: case-insensitive whatever the column
            // collation (and in the in-memory test provider).
            var term = search.Trim().ToLower();
            q = q.Where(u => u.Email.ToLower().Contains(term) || u.Username.ToLower().Contains(term));
        }

        if (blocked.HasValue)
            q = q.Where(u => u.IsBlocked == blocked.Value);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(u => u.CreatedAt)
            .ThenByDescending(u => u.Id)
            .Skip((currentPage - 1) * size)
            .Take(size)
            .Select(u => new AdminUserListItemDto
            {
                Id = u.Id,
                Email = u.Email,
                Name = u.Username,
                IsBlocked = u.IsBlocked,
                CreatedAt = u.CreatedAt,
                // A subquery in the same SELECT, not a query per user.
                CarCount = _db.Cars.Count(c => c.UserId == u.Id && c.DeletedAt == null),
            })
            .ToListAsync(ct);

        return Ok(new PagedResultDto<AdminUserListItemDto>
        {
            Total = total,
            Page = currentPage,
            PageSize = size,
            Items = items
        });
    }

    private static (int Page, int PageSize) NormalizePaging(int? page, int? pageSize) =>
        (page is > 0 ? page.Value : 1,
         pageSize is > 0 ? Math.Min(pageSize.Value, 50) : 20);
}
