using Entry.Services;

namespace CarListing.Tests;

/// <summary>
/// The rule parser is the free half of search. What it recognises never reaches
/// the model, so its coverage is the cost control.
/// </summary>
public class QueryRuleParserTests
{
    private static readonly SearchVocabulary Vocabulary = new(
        Brands: ["Toyota", "Land Cruiser Prado", "Ford", "Kia"],
        Models: ["Prado", "Focus"],
        FuelTypes: ["Benzin", "Dizel", "Hibrid"],
        BodyTypes: ["SUV", "Sedan", "Hetçbek"],
        Transmissions: ["Avtomat", "Mexaniki"],
        Cities: ["Bakı", "Gəncə", "Sumqayıt"]);

    private static RuleParse Parse(string query) => QueryRuleParser.Parse(query, Vocabulary);

    [Fact]
    public void ReadsTheCommonShape()
    {
        var parse = Parse("2015-dən yuxarı dizel SUV, 30 minə qədər");

        Assert.Equal("Dizel", parse.Filter.FuelType);
        Assert.Equal("SUV", parse.Filter.BodyType);
        Assert.Equal(2015, parse.Filter.YearMin);
        Assert.Equal(30_000, parse.Filter.PriceMax);
    }

    [Theory]
    [InlineData("diesel suv", "Dizel")]
    [InlineData("petrol sedan", "Benzin")]
    [InlineData("hybrid car", "Hibrid")]
    public void RecognisesAFuelTypeWrittenInEnglish(string query, string stored)
    {
        // The listings are filed in Azerbaijani, so an English word has to map
        // onto the stored spelling or it survives as free text and finds
        // nothing.
        var parse = Parse(query);

        Assert.Equal(stored, parse.Filter.FuelType);
        Assert.DoesNotContain("diesel", parse.Leftover, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecognisesABodyTypeWrittenInEnglish()
    {
        var parse = Parse("hatchback under 12000");

        Assert.Equal("Hetçbek", parse.Filter.BodyType);
        Assert.Equal(12_000, parse.Filter.PriceMax);
    }

    [Fact]
    public void RecognisesATransmissionWrittenInEnglish()
    {
        var parse = Parse("automatic sedan");

        Assert.Equal("Avtomat", parse.Filter.Transmission);
    }

    [Theory]
    [InlineData("bmw under 20000", 20_000)]
    [InlineData("toyota below 15000", 15_000)]
    [InlineData("sedan up to 30000", 30_000)]
    [InlineData("less than 25000", 25_000)]
    [InlineData("max 18000", 18_000)]
    [InlineData("up to 30k", 30_000)]
    public void ReadsAnEnglishPriceCeiling(string query, int expected)
    {
        // Without this the number stays in free text and the search looks for
        // the literal string "under 20000" in the listings.
        var parse = Parse(query);

        Assert.Equal(expected, parse.Filter.PriceMax);
        Assert.Contains("priceMax", parse.Matched);
    }

    [Theory]
    [InlineData("over 15000", 15_000)]
    [InlineData("above 20000", 20_000)]
    [InlineData("more than 12000", 12_000)]
    [InlineData("over 25k", 25_000)]
    public void ReadsAnEnglishPriceFloor(string query, int expected)
    {
        var parse = Parse(query);

        Assert.Equal(expected, parse.Filter.PriceMin);
        Assert.Contains("priceMin", parse.Matched);
    }

    [Fact]
    public void LeavesNoPriceWordsInTheLeftover()
    {
        // Leftover words decide whether the model is called at all, so a price
        // the rules understood must not still look unexplained.
        var parse = Parse("bmw under 20000");

        Assert.DoesNotContain("under", parse.Leftover, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("20000", parse.Leftover);
    }

    [Fact]
    public void MatchesTermsTypedWithoutAzerbaijaniLetters()
    {
        // A phone without an Azerbaijani keyboard produces exactly this.
        var parse = Parse("gencede avtomat sedan");

        Assert.Equal("Gəncə", parse.Filter.City);
        Assert.Equal("Avtomat", parse.Filter.Transmission);
        Assert.Equal("Sedan", parse.Filter.BodyType);
    }

    [Fact]
    public void PrefersTheLongerBrandName()
    {
        // "Land Cruiser Prado" must not lose to a shorter entry.
        var parse = Parse("Land Cruiser Prado Bakı");

        Assert.Equal("Land Cruiser Prado", parse.Filter.Brand);
    }

    [Fact]
    public void MatchesWholeWordsOnly()
    {
        // "kia" sits inside "makiyaj"; a substring hit would filter the results
        // to a brand the user never mentioned.
        var parse = Parse("makiyaj çantası");

        Assert.Null(parse.Filter.Brand);
    }

    [Fact]
    public void ReadsMileageBeforePrice()
    {
        // "100 min km" is a distance; read as money it becomes a 100000 AZN cap
        // and quietly hides every affordable car.
        var parse = Parse("100 min km-dən az benzin sedan");

        Assert.Equal(100_000, parse.Filter.MileageMax);
        Assert.Null(parse.Filter.PriceMax);
    }

    [Fact]
    public void ReadsAPriceRange()
    {
        var parse = Parse("Toyota 18-22 min");

        Assert.Equal("Toyota", parse.Filter.Brand);
        Assert.Equal(18_000, parse.Filter.PriceMin);
        Assert.Equal(22_000, parse.Filter.PriceMax);
    }

    [Fact]
    public void ReadsAYearRange()
    {
        var parse = Parse("2016-2020 Ford");

        Assert.Equal(2016, parse.Filter.YearMin);
        Assert.Equal(2020, parse.Filter.YearMax);
    }

    [Fact]
    public void ReadsAYearCeiling()
    {
        var parse = Parse("2019-a qədər sedan");

        Assert.Equal(2019, parse.Filter.YearMax);
        Assert.Null(parse.Filter.YearMin);
    }

    [Fact]
    public void TreatsCheapAsAnOrderNotABudget()
    {
        // "ucuz" says how to sort. Inventing a price cap from it would hide
        // cars the user never excluded.
        var parse = Parse("Gəncədə ucuz hibrid");

        Assert.Equal("price", parse.Filter.SortBy);
        Assert.Equal("asc", parse.Filter.SortDirection);
        Assert.Null(parse.Filter.PriceMax);
        Assert.Equal("Hibrid", parse.Filter.FuelType);
        Assert.Equal("Gəncə", parse.Filter.City);
    }

    [Fact]
    public void LeavesNoSuffixBehindWhenAWordIsMatched()
    {
        // "Gəncədə" must be consumed whole. Removing only the stem strands
        // "də", which then rides along in Search and filters the results by a
        // fragment the user never typed.
        var parse = Parse("Gəncədə hibrid");

        Assert.Equal("Gəncə", parse.Filter.City);
        Assert.Equal("Hibrid", parse.Filter.FuelType);
        Assert.Null(parse.Filter.Search);
    }

    [Fact]
    public void ConsumesTheWordsThatQualifyAMileage()
    {
        // "-dən az" belongs to the mileage, not to the free text.
        var parse = Parse("benzin sedan, 100 min km-dən az");

        Assert.Equal(100_000, parse.Filter.MileageMax);
        Assert.Null(parse.Filter.Search);
    }

    [Fact]
    public void ReportsWordsItCouldNotUse()
    {
        var parse = Parse("Toyota təmiz salon bir sahibdən");

        Assert.Equal("Toyota", parse.Filter.Brand);
        Assert.Contains("temiz", parse.Leftover);
    }

    [Fact]
    public void MatchesNothingInASentenceItDoesNotUnderstand()
    {
        var parse = Parse("uşaq üçün rahat və etibarlı nəsə lazımdır");

        Assert.Empty(parse.Matched);
    }

    [Fact]
    public void IgnoresABrandNobodyHasListed()
    {
        // The vocabulary is the live data, so a filter can only name something
        // that exists to be found.
        var parse = Parse("Ferrari Bakı");

        Assert.Null(parse.Filter.Brand);
        Assert.Equal("Bakı", parse.Filter.City);
    }
}

/// <summary>
/// Where the money is decided: every query this lets through is a paid call,
/// and every query it stops is one the user still has to be served well.
/// </summary>
public class RuleParseVerdictTests
{
    private static readonly SearchVocabulary Vocabulary = new(
        Brands: ["Toyota"],
        Models: ["Prado"],
        FuelTypes: ["Dizel"],
        BodyTypes: ["SUV"],
        Transmissions: ["Avtomat"],
        Cities: ["Bakı"]);

    private static bool NeedsModel(string query, int limit = 2) =>
        RuleParseVerdict.NeedsModel(QueryRuleParser.Parse(query, Vocabulary), limit);

    [Fact]
    public void AnswersAFullyUnderstoodQueryWithoutTheModel()
    {
        Assert.False(NeedsModel("2015-dən yuxarı dizel SUV, 30 minə qədər"));
    }

    [Fact]
    public void EscalatesWhenTheRulesUnderstoodNothing()
    {
        // Escalating costs a fraction of a cent; returning nothing costs a user.
        Assert.True(NeedsModel("uşaq üçün rahat və etibarlı nəsə lazımdır"));
    }

    [Fact]
    public void ToleratesAModelNameAlongsideAMatch()
    {
        // "Prado" is not in the vocabulary but rides along as free text, and
        // that is not worth a round trip.
        Assert.False(NeedsModel("Toyota Prado dizel"));
    }

    [Fact]
    public void EscalatesWhenMostOfTheSentenceWentUnread()
    {
        Assert.True(NeedsModel("Toyota amma ailə üçün geniş və qənaətcil olsun"));
    }

    [Fact]
    public void LimitOfZeroSendsAnythingWithLeftoverWords()
    {
        // The dial that trades money for understanding.
        Assert.True(NeedsModel("Toyota Prado", limit: 0));
        Assert.False(NeedsModel("Toyota Prado", limit: 1));
    }
}
