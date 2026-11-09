namespace Entry.Concrete;

/// <summary>
/// A single-use password reset token. Only a SHA-256 hash of the token is
/// stored, so a leaked database cannot be used to reset anyone's password.
/// </summary>
public class PasswordResetToken : Base
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Lowercase hex SHA-256 of the token sent by email.</summary>
    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    /// <summary>Set once the token is used, or when another reset makes it obsolete.</summary>
    public DateTime? UsedAt { get; set; }
}
