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
    public string? Color { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public ICollection<CarImage> Images { get; set; } = new List<CarImage>();
    public ICollection<CarFeature> Features { get; set; } = new List<CarFeature>();
}
