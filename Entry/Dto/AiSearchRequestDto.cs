using System.ComponentModel.DataAnnotations;

namespace Entry.Dto;

public class AiSearchRequestDto
{
    [Required]
    [StringLength(500, MinimumLength = 2)]
    public string Query { get; set; } = null!;
}
