namespace Entry.Services;

/// <summary>The "Smtp" config section.</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? Password { get; set; }
    public string? From { get; set; }
    public bool EnableSsl { get; set; } = true;

    /// <summary>Host and From are the minimum needed to send anything.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}

/// <summary>The "Frontend" config section.</summary>
public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    /// <summary>Origin of the React app, e.g. http://localhost:5173. Links in emails point here.</summary>
    public string? BaseUrl { get; set; }
}
