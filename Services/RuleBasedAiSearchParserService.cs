using System.Text.RegularExpressions;
using Entry.Dto;

namespace Entry.Services;

public sealed class RuleBasedAiSearchParserService : IAiSearchParserService
{
    private static readonly string[] KnownBrands =
    [
        "Toyota", "Honda", "Mercedes-Benz", "BMW", "Hyundai", "Kia", "Nissan", "Ford", "Audi",
        "Volkswagen", "Chevrolet", "Opel", "Lada"
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

        // Notes reach the buyer, so they carry only what was understood -
        // never a remark about which parser ran.
        var notes = new List<string>();

        foreach (var brand in KnownBrands)
        {
            if (normalized.Contains(brand.ToLowerInvariant()))
            {
                filter.Brand = brand;
                notes.Add($"Brend: {brand}");
                break;
            }
        }

        var priceRange = Regex.Match(normalized, @"(?<min>\d{1,3})\s*[-–]\s*(?<max>\d{1,3})\s*(min|k|000)?");
        if (priceRange.Success)
        {
            filter.PriceMin = ToMoney(priceRange.Groups["min"].Value);
            filter.PriceMax = ToMoney(priceRange.Groups["max"].Value);
            notes.Add($"Qiymət: {filter.PriceMin:0}-{filter.PriceMax:0} AZN");
        }

        var yearMin = Regex.Match(normalized, @"(?<year>20\d{2}|19\d{2})\s*(\+|dən yuxarı|den yuxari|sonra)");
        if (yearMin.Success)
        {
            filter.YearMin = int.Parse(yearMin.Groups["year"].Value);
            notes.Add($"İl: {filter.YearMin}-dən yuxarı");
        }

        // The user may type either language, but the value written into the
        // filter has to be the one the listings actually store, which is
        // Azerbaijani. Anything else silently matches no rows.
        if (normalized.Contains("sedan"))
            filter.BodyType = "Sedan";
        else if (normalized.Contains("suv") || normalized.Contains("krossover") || normalized.Contains("crossover"))
            filter.BodyType = "SUV";
        else if (normalized.Contains("hetçbek") || normalized.Contains("hetcbek") || normalized.Contains("hatchback"))
            filter.BodyType = "Hetçbek";
        else if (normalized.Contains("kupe") || normalized.Contains("coupe"))
            filter.BodyType = "Kupe";
        else if (normalized.Contains("universal") || normalized.Contains("wagon"))
            filter.BodyType = "Universal";
        else if (normalized.Contains("miniven") || normalized.Contains("minivan"))
            filter.BodyType = "Miniven";
        else if (normalized.Contains("pikap") || normalized.Contains("pickup"))
            filter.BodyType = "Pikap";

        if (normalized.Contains("avtomat") || normalized.Contains("automatic"))
            filter.Transmission = "Avtomat";
        else if (normalized.Contains("variator"))
            filter.Transmission = "Variator";
        else if (normalized.Contains("mexanik") || normalized.Contains("manual"))
            filter.Transmission = "Mexaniki";

        if (normalized.Contains("benzin") || normalized.Contains("petrol"))
            filter.FuelType = "Benzin";
        else if (normalized.Contains("dizel") || normalized.Contains("diesel"))
            filter.FuelType = "Dizel";
        else if (normalized.Contains("hibrid") || normalized.Contains("hybrid"))
            filter.FuelType = "Hibrid";
        else if (normalized.Contains("elektrik") || normalized.Contains("electric"))
            filter.FuelType = "Elektrik";

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
