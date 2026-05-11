using System.ComponentModel.DataAnnotations;

namespace Entry.Dto;

public class LoginDto
{
    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;
}
