namespace Entry.Dto;

public class AiSearchResultDto
{
    public string Query { get; set; } = null!;
    public CarSearchFilterDto Filter { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}
