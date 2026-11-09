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
    public int Mileage { get; set; }
    public string FuelType { get; set; } = null!;
    public string BodyType { get; set; } = null!;
    public string Transmission { get; set; } = null!;
    public string Engine { get; set; } = null!;
    public string City { get; set; } = null!;
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>The main image, else the first by order, else null.</summary>
    public string? MainImageUrl { get; set; }

    public List<CarFeatureDto> Features { get; set; } = new();
}
