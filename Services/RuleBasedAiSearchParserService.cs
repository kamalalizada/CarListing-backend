using System.Text.RegularExpressions;
using Entry.Dto;

namespace Entry.Services;

public sealed class RuleBasedAiSearchParserService : IAiSearchParserService
{
    private static readonly string[] KnownBrands =
    [
        "Toyota", "Honda", "Mercedes", "BMW", "Hyundai", "Kia", "Nissan", "Ford", "Audi", "Volkswagen"
    ];

    public Task<AiSearchResultDto> ParseAsync(
        AiSearchRequestDto request,
        CancellationToken ct = default)
    {
        return Task.FromResult(Parse(request));
    }

    /// <summary>Pure, synchronous parse - no I/O, so it stays directly testable
    /// and can serve as the fallback when the model call fails.</summary>
    public static AiSearchResultDto Parse(AiSearchRequestDto request)
    {
        var query = request.Query.Trim();
        var normalized = query.ToLowerInvariant();
        var filter = new CarSearchFilterDto
        {
            Search = query,
            SortBy = "newest",
            SortDirection = "desc"
        };

        var notes = new List<string>
        {
            "Rule-based parser MVP işləyir. Sonra bunu AI parser ilə əvəz etmək olar."
        };

        foreach (var brand in KnownBrands)
        {
            if (normalized.Contains(brand.ToLowerInvariant()))
            {
                filter.Brand = brand;
                notes.Add($"Brand tapıldı: {brand}.");
                break;
            }
        }

        var priceRange = Regex.Match(normalized, @"(?<min>\d{1,3})\s*[-–]\s*(?<max>\d{1,3})\s*(min|k|000)?");
        if (priceRange.Success)
        {
            filter.PriceMin = ToMoney(priceRange.Groups["min"].Value);
            filter.PriceMax = ToMoney(priceRange.Groups["max"].Value);
            notes.Add("Qiymət aralığı tapıldı.");
        }

        var yearMin = Regex.Match(normalized, @"(?<year>20\d{2}|19\d{2})\s*(\+|dən yuxarı|den yuxari|sonra)");
        if (yearMin.Success)
        {
            filter.YearMin = int.Parse(yearMin.Groups["year"].Value);
            notes.Add("Minimum il tapıldı.");
        }

        if (normalized.Contains("sedan"))
            filter.BodyType = "Sedan";
        else if (normalized.Contains("suv") || normalized.Contains("crossover"))
            filter.BodyType = "SUV";
        else if (normalized.Contains("hetçbek") || normalized.Contains("hatchback"))
            filter.BodyType = "Hatchback";

        if (normalized.Contains("avtomat") || normalized.Contains("automatic"))
            filter.Transmission = "Automatic";
        else if (normalized.Contains("mexanika") || normalized.Contains("manual"))
            filter.Transmission = "Manual";

        if (normalized.Contains("benzin") || normalized.Contains("petrol"))
            filter.FuelType = "Petrol";
        else if (normalized.Contains("dizel") || normalized.Contains("diesel"))
            filter.FuelType = "Diesel";
        else if (normalized.Contains("hybrid") || normalized.Contains("hibrid"))
            filter.FuelType = "Hybrid";
        else if (normalized.Contains("elektrik") || normalized.Contains("electric"))
            filter.FuelType = "Electric";

        return new AiSearchResultDto
        {
            Query = query,
            Filter = filter,
            Notes = notes
        };
    }

    private static decimal ToMoney(string value)
    {
        return decimal.Parse(value) * 1000;
    }
}
