namespace Entry.Dto;

/// <summary>An item of GET /api/admin/cars.</summary>
public class AdminCarListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string Brand { get; set; } = null!;
    public string Model { get; set; } = null!;
    public int Year { get; set; }
    public decimal Price { get; set; }
    public int Mileage { get; set; }
    public string FuelType { get; set; } = null!;
    public string BodyType { get; set; } = null!;
    public string Transmission { get; set; } = null!;
    public string Engine { get; set; } = null!;
    public string City { get; set; } = null!;
    public string? Color { get; set; }

    /// <summary>Same value as <see cref="OwnerId"/>; kept for existing clients.</summary>
    public int UserId { get; set; }

    public bool IsActive { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public int OwnerId { get; set; }

    /// <summary>The owner's username.</summary>
    public string OwnerName { get; set; } = null!;

    public string OwnerEmail { get; set; } = null!;
}

/// <summary>An item of GET /api/admin/users.</summary>
public class AdminUserListItemDto
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;

    /// <summary>The username.</summary>
    public string Name { get; set; } = null!;

    public bool IsBlocked { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>The user's listings, deleted ones excluded.</summary>
    public int CarCount { get; set; }
}
