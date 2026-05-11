using System.ComponentModel.DataAnnotations;

namespace Entry.Dto;

public class CreateCarFeatureDto
{
    [Required]
    [StringLength(100)]
    public string Key { get; set; } = null!;

    [Required]
    [StringLength(200)]
    public string Value { get; set; } = null!;
}
