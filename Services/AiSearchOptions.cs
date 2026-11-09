using System.ComponentModel.DataAnnotations;

namespace Entry.Services;

/// <summary>
/// Settings for the natural-language search parser.
/// </summary>
/// <remarks>
/// The API key is deliberately absent. It is read from the
/// <c>GEMINI_API_KEY</c> environment variable, so it never
/// sits in <c>appsettings.json</c> where it would be committed to git.
/// </remarks>
public sealed class AiSearchOptions
{
    public const string SectionName = "AiSearch";

    /// <summary>
    /// Turns the model off without a redeploy; requests fall back to the rule
    /// parser. Also the effective state when no API key is configured.
    /// </summary>
    public bool Enabled { get; set; } = true;

    [Required]
    public string Model { get; set; } = "gemini-2.5-flash-lite";

    /// <summary>
    /// A search slower than this is worse than a cruder one: the buyer is
    /// staring at a spinner, and the rule parser answers instantly.
    /// </summary>
    /// <remarks>
    /// A local 7B model is the slow case. Measured here it answers in 3-15
    /// seconds once loaded, and the first call after a restart pays several
    /// more while the weights are read into memory, so the ceiling is set well
    /// above the typical response rather than at it.
    /// </remarks>
    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 12;

    /// <summary>
    /// Where Ollama is listening, or empty to leave it off.
    /// </summary>
    /// <remarks>
    /// Empty in <c>appsettings.json</c> and set only in the Development file,
    /// because Ollama runs on a developer's own machine. A deployed instance
    /// has nothing at this address, and pointing at one would make every search
    /// wait for a connection that cannot succeed before falling back.
    /// </remarks>
    public string OllamaBaseUrl { get; set; } = "";

    /// <summary>
    /// The Ollama model to pull and run, e.g. <c>qwen2.5:7b</c>.
    /// </summary>
    public string OllamaModel { get; set; } = "qwen2.5:7b";

    /// <summary>
    /// How many unrecognised words a rule parse may leave before the query is
    /// handed to the model.
    /// </summary>
    /// <remarks>
    /// Raising this spends less and understands less; lowering it spends more
    /// and understands more. Two tolerates the filler people habitually type
    /// ("satılır", "təcili") and a model name, which still narrows results as
    /// free text.
    /// </remarks>
    [Range(0, 10)]
    public int RuleLeftoverWordLimit { get; set; } = 2;
}
