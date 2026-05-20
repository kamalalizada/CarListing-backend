namespace Entry.Dto;

public class CarMetadataDto
{
    public List<string> FuelTypes { get; set; } = new();
    public List<string> BodyTypes { get; set; } = new();
    public List<string> Transmissions { get; set; } = new();
    public List<string> SortOptions { get; set; } = new();
}
