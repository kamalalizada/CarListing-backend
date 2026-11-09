namespace Entry.Concrete;

public class Car : Base
{
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
    public string ContactPhone { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// Publicly visible. False both when an admin deactivated the listing and
    /// when its owner deleted it; <see cref="DeletedAt"/> tells the two apart.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Set when the owner (or an admin) deletes the listing. A deleted car is
    /// gone for everyone; an admin re-activation must not bring it back.
    /// Invariant: DeletedAt != null implies IsActive == false.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public ICollection<CarImage> Images { get; set; } = new List<CarImage>();
    public ICollection<CarFeature> Features { get; set; } = new List<CarFeature>();
}
