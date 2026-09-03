using Entry.Dto;

namespace Entry.Services;

public interface IAiSearchParserService
{
    /// <summary>
    /// Turns a free-text query into a structured filter.
    /// </summary>
    /// <remarks>
    /// Async because an implementation may call a language model over the
    /// network; a synchronous signature would block a request thread for the
    /// whole round trip.
    /// </remarks>
    Task<AiSearchResultDto> ParseAsync(AiSearchRequestDto request, CancellationToken ct = default);
}
