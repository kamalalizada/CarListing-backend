using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entry.Dto;
public class CarListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string Brand { get; set; } = null!;
    public string Model { get; set; } = null!;
    public int Year { get; set; }
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<CarFeatureDto> Features { get; set; } = new();
}
