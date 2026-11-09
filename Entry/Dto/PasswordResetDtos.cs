namespace Entry.Dto;

// Properties are nullable on purpose: with non-nullable strings, [ApiController]
// would reject a missing field with ValidationProblemDetails before the action
// runs, and these endpoints answer with { message } instead.

public class ForgotPasswordDto
{
    public string? Email { get; set; }
}

public class ResetPasswordDto
{
    public string? Email { get; set; }
    public string? Token { get; set; }
    public string? NewPassword { get; set; }
}
