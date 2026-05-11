using System.ComponentModel.DataAnnotations;

namespace Entry.Dto;

public class RegisterDto
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = null!;

    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = null!;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = null!;
}
