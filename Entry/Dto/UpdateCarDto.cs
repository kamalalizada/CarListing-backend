using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entry.Dto;
public class UpdateCarDto
{
    public string Title { get; set; } = null!;
    public string Brand { get; set; } = null!;
    public string Model { get; set; } = null!;
    public int Year { get; set; }
    public decimal Price { get; set; }

    public List<CreateCarFeatureDto> Features { get; set; } = new();
}
