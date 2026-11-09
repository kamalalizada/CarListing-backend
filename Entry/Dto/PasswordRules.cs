using System.ComponentModel.DataAnnotations;

namespace Entry.Dto;

/// <summary>
/// The one definition of a valid password, shared by register and reset so the
/// two can never disagree.
/// </summary>
public static class PasswordRules
{
    public const int MinLength = 6;
    public const int MaxLength = 100;

    public const string RequiredMessage = "Şifrə tələb olunur.";
    public const string LengthMessage = "Şifrə 6-100 simvol arasında olmalıdır.";

    /// <summary>Null when valid, otherwise the message to show.</summary>
    public static string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return RequiredMessage;

        if (password.Length < MinLength || password.Length > MaxLength)
            return LengthMessage;

        return null;
    }
}

/// <summary>
/// Applies the length rule of <see cref="PasswordRules"/> during model
/// validation. Pair it with <c>[Required(ErrorMessage = PasswordRules.RequiredMessage)]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PasswordAttribute : ValidationAttribute
{
    public override bool RequiresValidationContext => false;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // Blank is [Required]'s job; reporting it here too would list the
        // same error twice.
        if (value is not string password || string.IsNullOrWhiteSpace(password))
            return ValidationResult.Success;

        var error = PasswordRules.Validate(password);
        return error is null
            ? ValidationResult.Success
            : new ValidationResult(error, validationContext.MemberName is null ? null : [validationContext.MemberName]);
    }
}
