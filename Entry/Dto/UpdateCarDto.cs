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

    [Range(typeof(decimal), "0.01", "9999999999999999", ParseLimitsInInvariantCulture = true)]
    public decimal Price { get; set; }

    [Range(0, 2_000_000)]
    public int Mileage { get; set; }

    [Required]
    [StringLength(30)]
    public string FuelType { get; set; } = null!;

    [Required]
    [StringLength(30)]
    public string BodyType { get; set; } = null!;

    [Required]
    [StringLength(30)]
    public string Transmission { get; set; } = null!;

    [Required]
    [StringLength(50)]
    public string Engine { get; set; } = null!;

    [Required]
    [StringLength(50)]
    public string City { get; set; } = null!;

    [Required]
    [StringLength(20, MinimumLength = 7)]
    [RegularExpression(@"^\+?[0-9][0-9\s-]{5,18}$", ErrorMessage = "Telefon nömrəsini düzgün formatda daxil edin.")]
    public string ContactPhone { get; set; } = null!;

    [StringLength(50)]
    public string? Color { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    public List<CreateCarFeatureDto> Features { get; set; } = new();
}
