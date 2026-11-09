namespace Entry.Dto;

/// <summary>The values of <see cref="MyCarListItemDto.Status"/>.</summary>
public static class CarListingStatus
{
    public const string Active = "Active";

    /// <summary>An admin turned the listing off; it is not public.</summary>
    public const string Deactivated = "Deactivated";
}

/// <summary>An item of GET /api/cars/mine: a list item plus its status.</summary>
public class MyCarListItemDto : CarListItemDto
{
    /// <summary>"Active" or "Deactivated" (see <see cref="CarListingStatus"/>).</summary>
    public string Status { get; set; } = null!;
}
