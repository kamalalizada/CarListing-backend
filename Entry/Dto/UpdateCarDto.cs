using System.ComponentModel.DataAnnotations;

namespace Entry.Dto;

public class UpdateCarDto
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = null!;

    [Required]
    [StringLength(50)]
    public string Brand { get; set; } = null!;

    [Required]
    [StringLength(50)]
    public string Model { get; set; } = null!;

    [Range(1950, 2100)]
    public int Year { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal Price { get; set; }

    public List<CreateCarFeatureDto> Features { get; set; } = new();
}
