namespace Entry.Concrete;

public class CarFeature : Base
{
    public int CarId { get; set; }
    public Car Car { get; set; } = null!;

    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
}
