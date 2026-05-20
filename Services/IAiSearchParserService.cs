using Entry.Dto;

namespace Entry.Services;

public interface IAiSearchParserService
{
    AiSearchResultDto Parse(AiSearchRequestDto request);
}
