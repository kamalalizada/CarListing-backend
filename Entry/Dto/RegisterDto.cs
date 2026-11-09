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

    /// <summary>See <see cref="PasswordRules"/>: 6-100 characters, not blank.</summary>
    [Required(ErrorMessage = PasswordRules.RequiredMessage)]
    [Password]
    public string Password { get; set; } = null!;
}
