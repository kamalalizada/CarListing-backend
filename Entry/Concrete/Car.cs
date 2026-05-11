namespace Entry.Concrete;

public class Car : Base
{
    public string Title { get; set; } = null!;
    public string Brand { get; set; } = null!;
    public string Model { get; set; } = null!;
    public int Year { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public ICollection<CarImage> Images { get; set; } = new List<CarImage>();
    public ICollection<CarFeature> Features { get; set; } = new List<CarFeature>();
}
