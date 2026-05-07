namespace Entry.Concrete;

public class CarFeature : Base
{
    public int CarId { get; set; }
    public Car Car { get; set; }

    public string Key { get; set; }
    public string Value { get; set; }

}



