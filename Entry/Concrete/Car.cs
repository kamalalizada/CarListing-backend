using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entry.Concrete;
public class Car : Base
{
    public string Title { get; set; }
    public string Brand { get; set; }
    public string Model { get; set; }
    public int Year { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;

    public int UserId { get; set; }
    public User User { get; set; }

    public ICollection<CarImage> Images { get; set; }
    public ICollection<CarFeature> Features { get; set; }

}
