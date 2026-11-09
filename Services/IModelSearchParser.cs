using Entry.Dto;

namespace Entry.Services;

/// <summary>
/// The model half of the search pipeline, whichever model that is.
/// </summary>
/// <remarks>
/// Separate from <see cref="IAiSearchParserService"/> on purpose. That
/// interface is what the controller resolves and is answered by
/// <see cref="HybridAiSearchParserService"/>; this one is what the hybrid
/// escalates to. Keeping them apart means the hybrid depends on the idea of a
/// model rather than on a particular vendor, so swapping Ollama for a hosted
/// API — or running neither — changes one registration in Program.cs and
/// nothing else.
/// </remarks>
public interface IModelSearchParser
{
    Task<AiSearchResultDto> ParseAsync(AiSearchRequestDto request, CancellationToken ct = default);
}
