namespace Entry.Dto;

public class CarSearchFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? City { get; set; }
    public string? FuelType { get; set; }
    public string? BodyType { get; set; }
    public string? Transmission { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public int? YearMin { get; set; }
    public int? YearMax { get; set; }
    public int? MileageMax { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}
