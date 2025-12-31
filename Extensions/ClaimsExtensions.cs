using System.Security.Claims;

namespace Entry.Extensions;

public static class ClaimsExtensions
{
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var idStr =
            user.FindFirstValue(ClaimTypes.NameIdentifier) ??
            user.FindFirstValue("sub") ??
            user.FindFirstValue("id") ??
            user.FindFirstValue("userId");

        if (string.IsNullOrWhiteSpace(idStr))
            throw new UnauthorizedAccessException("Token-də user id claim tapılmadı (NameIdentifier/sub). TokenService-də claim əlavə et.");

        if (!int.TryParse(idStr, out var id))
            throw new UnauthorizedAccessException($"Token-də user id numeric deyil: '{idStr}'");

        return id;
    }
}
