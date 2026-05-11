namespace Entry.Concrete;

public class CarImage : Base
{
    public string ImageUrl { get; set; } = null!;
    public string? ObjectKey { get; set; }
    public bool IsMain { get; set; }
    public int Order { get; set; }

    public int CarId { get; set; }
    public Car Car { get; set; } = null!;
}
