using System.Security.Claims;
using Entry.Data;
using Microsoft.EntityFrameworkCore;

namespace Entry.Services;

public sealed class BlockedUserTokenValidator
{
    private readonly AppDbContext _db;

    public BlockedUserTokenValidator(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsBlockedAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var idValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idValue, out var userId))
            return false;

        return await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.IsBlocked, cancellationToken);
    }
}
