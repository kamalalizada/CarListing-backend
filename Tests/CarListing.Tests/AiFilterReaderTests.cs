using Entry.Services;

namespace CarListing.Tests;

/// <summary>
/// The model's answer is untrusted input. These cover what happens when it is
/// wrong, not when it is right.
/// </summary>
public class AiFilterReaderTests
{
    private static readonly SearchVocabulary Vocabulary = new(
        Brands: ["Ford", "Toyota"],
        Models: ["Focus", "Prado"],
        FuelTypes: ["Benzin", "Dizel"],
        BodyTypes: ["SUV", "Sedan"],
        Transmissions: ["Avtomat", "Mexaniki"],
        Cities: ["Bakı", "Gəncə"]);

    /// <summary>
    /// The query defaults to something that contains the free-text terms these
    /// tests use, because <c>search</c> is only kept when it echoes the user.
    /// </summary>
    private static Entry.Dto.AiSearchResultDto Read(string json, string query = "test sorğusu təmiz salon") =>
        AiFilterReader.Read(query, json, Vocabulary);

    [Fact]
    public void KeepsValuesThatExistInTheListings()
    {
        var result = Read("""
            {"brand":"Toyota","fuelType":"Dizel","bodyType":"SUV",
             "transmission":"Avtomat","city":"Bakı","priceMax":30000,"yearMin":2015}
            """,
            "2015-dən yuxarı dizel avtomat SUV Toyota, Bakı, 30 minə qədər");

        Assert.Equal("Toyota", result.Filter.Brand);
        Assert.Equal("Dizel", result.Filter.FuelType);
        Assert.Equal("SUV", result.Filter.BodyType);
        Assert.Equal("Avtomat", result.Filter.Transmission);
        Assert.Equal("Bakı", result.Filter.City);
        Assert.Equal(30_000, result.Filter.PriceMax);
        Assert.Equal(2015, result.Filter.YearMin);
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void MatchesVocabularyIgnoringCaseAndReturnsTheStoredSpelling()
    {
        // A model that writes "bakı" still means the stored "Bakı"; the filter
        // has to carry the spelling the rows use or it matches nothing.
        var result = Read("""{"brand":"toyota","city":"bakı"}""");

        Assert.Equal("Toyota", result.Filter.Brand);
        Assert.Equal("Bakı", result.Filter.City);
    }

    [Fact]
    public void DropsAValueTheListingsDoNotContainAndSaysSo()
    {
        var result = Read("""{"brand":"Ferrari"}""");

        Assert.Null(result.Filter.Brand);
        Assert.Contains(result.Notes, note => note.Contains("Ferrari"));
    }

    [Fact]
    public void RepairsAReversedPriceRange()
    {
        // Reversed, this filter matches nothing; the user meant a range either
        // way round.
        var result = Read("""{"priceMin":40000,"priceMax":10000}""");

        Assert.Equal(10_000, result.Filter.PriceMin);
        Assert.Equal(40_000, result.Filter.PriceMax);
    }

    [Fact]
    public void RepairsAReversedYearRange()
    {
        var result = Read("""{"yearMin":2022,"yearMax":2016}""");

        Assert.Equal(2016, result.Filter.YearMin);
        Assert.Equal(2022, result.Filter.YearMax);
    }

    [Theory]
    [InlineData(1800)]   // before cars existed
    [InlineData(3000)]   // a price misread as a year
    public void RejectsAnImplausibleYear(int year)
    {
        var result = Read($$"""{"yearMin":{{year}}}""");

        Assert.Null(result.Filter.YearMin);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5000)]
    public void RejectsANonPositivePrice(int price)
    {
        var result = Read($$"""{"priceMax":{{price}}}""");

        Assert.Null(result.Filter.PriceMax);
    }

    [Fact]
    public void DropsASortDirectionWithNothingToSortBy()
    {
        var result = Read("""{"sortDirection":"asc"}""");

        Assert.Null(result.Filter.SortBy);
        Assert.Null(result.Filter.SortDirection);
    }

    [Fact]
    public void DefaultsPriceSortToCheapestFirst()
    {
        var result = Read("""{"sortBy":"price"}""");

        Assert.Equal("price", result.Filter.SortBy);
        Assert.Equal("asc", result.Filter.SortDirection);
    }

    [Fact]
    public void DefaultsOtherSortsToNewestFirst()
    {
        var result = Read("""{"sortBy":"year"}""");

        Assert.Equal("desc", result.Filter.SortDirection);
    }

    [Fact]
    public void RejectsASortFieldTheQueryLayerDoesNotSupport()
    {
        var result = Read("""{"sortBy":"colour"}""");

        Assert.Null(result.Filter.SortBy);
    }

    [Fact]
    public void TreatsBlankStringsAsAbsent()
    {
        // An empty Search would widen nothing but still ride along in the URL.
        var result = Read("""{"brand":"   ","search":""}""");

        Assert.Null(result.Filter.Brand);
        Assert.Null(result.Filter.Search);
    }

    [Fact]
    public void IgnoresNullsRatherThanFailing()
    {
        var result = Read("""
            {"brand":null,"city":null,"priceMax":null,"yearMin":null,"note":null}
            """);

        Assert.Null(result.Filter.Brand);
        Assert.Null(result.Filter.PriceMax);
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void SurvivesAMissingField()
    {
        // Schema-constrained output should always be complete, but a filter is
        // not worth a 500 if it is not.
        var result = Read("""{"brand":"Ford"}""");

        Assert.Equal("Ford", result.Filter.Brand);
        Assert.Null(result.Filter.PriceMax);
    }

    [Fact]
    public void KeepsWordsThatBecameNoOtherFieldAsFreeText()
    {
        var result = Read("""{"search":"təmiz salon"}""", "təmiz salon axtarıram");

        Assert.Equal("təmiz salon", result.Filter.Search);
    }

    [Fact]
    public void DropsFreeTextTheUserNeverTyped()
    {
        // A small model translates "red" instead of copying it; searching for a
        // word nobody wrote matches nothing while looking like it worked.
        var result = Read("""{"search":"qırmızı"}""", "give me a red car");

        Assert.Null(result.Filter.Search);
    }

    [Fact]
    public void KeepsOnlyTheFreeTextTermsThatCameFromTheUser()
    {
        var result = Read("""{"search":"red qırmızı"}""", "give me a red car");

        Assert.Equal("red", result.Filter.Search);
    }

    [Fact]
    public void DropsAListedBodyTypeTheSentenceNeverAskedFor()
    {
        // "convertible" is not sold here, and a small model answers with the
        // nearest listed body rather than null.
        var result = Read(
            """{"bodyType":"Hetçbek"}""",
            "give me a fast red convertible from around 2019");

        Assert.Null(result.Filter.BodyType);
        Assert.Contains(result.Notes, n => n.Contains("Hetçbek"));
    }

    [Fact]
    public void KeepsABodyTypeTheSentenceNames()
    {
        var result = Read("""{"bodyType":"Sedan"}""", "2019 sedan up to 40000");

        Assert.Equal("Sedan", result.Filter.BodyType);
    }

    [Fact]
    public void KeepsAValueWrittenWithAnAzerbaijaniSuffix()
    {
        // "avtomatik" is the same word the listings store as "Avtomat".
        var result = Read("""{"transmission":"Avtomat"}""", "avtomatik sedan axtarıram");

        Assert.Equal("Avtomat", result.Filter.Transmission);
    }

    [Fact]
    public void KeepsAValueTheUserWroteInEnglish()
    {
        var result = Read("""{"fuelType":"Dizel"}""", "looking for a diesel SUV");

        Assert.Equal("Dizel", result.Filter.FuelType);
    }

    [Fact]
    public void DropsABrandTheSentenceRuledOut()
    {
        // Filtering for the brand the user rejected returns exactly the cars
        // they did not want - the opposite of their request.
        var result = Read(
            """{"brand":"Ford"}""",
            "i want to buy car but i do not want ford");

        Assert.Null(result.Filter.Brand);
        Assert.Contains(result.Notes, n => n.Contains("Ford"));
    }

    [Fact]
    public void DropsARejectedWordFromFreeText()
    {
        var result = Read(
            """{"search":"ford"}""",
            "i want to buy car but i do not want ford");

        Assert.Null(result.Filter.Search);
    }

    [Fact]
    public void DropsARejectedBodyTypeWrittenInAzerbaijani()
    {
        var result = Read("""{"bodyType":"SUV"}""", "SUV olmasın, sedan istəyirəm");

        Assert.Null(result.Filter.BodyType);
    }

    [Fact]
    public void KeepsWhatTheSecondClauseAsksFor()
    {
        // "SUV olmasın, sedan istəyirəm" rules out the SUV and still asks for
        // the sedan; rejecting both leaves the buyer with no filter at all.
        var result = Read("""{"bodyType":"Sedan"}""", "SUV olmasın, sedan istəyirəm");

        Assert.Equal("Sedan", result.Filter.BodyType);
    }

    [Fact]
    public void KeepsValuesStatedBeforeTheNegation()
    {
        // "diesel" is asked for; only what follows "not" is rejected.
        var result = Read(
            """{"fuelType":"Dizel","brand":"Ford"}""",
            "diesel car, not Ford");

        Assert.Equal("Dizel", result.Filter.FuelType);
        Assert.Null(result.Filter.Brand);
    }

    [Fact]
    public void TreatsNegationOnlyAsAWholeWord()
    {
        // "another" contains "not" but rejects nothing.
        var result = Read("""{"brand":"Ford"}""", "another Ford please");

        Assert.Equal("Ford", result.Filter.Brand);
    }

    [Fact]
    public void DropsAModelTheListingsDoNotContain()
    {
        var result = Read("""{"model":"Mustang"}""", "I want a Mustang");

        Assert.Null(result.Filter.Model);
        Assert.Contains(result.Notes, n => n.Contains("Mustang"));
    }

    [Fact]
    public void PutsTheModelsOwnRemarksFirst()
    {
        // The remarks explain the query as a whole; rejected values follow them.
        var result = Read("""{"brand":"Ferrari","notes":["Brend: Ferrari","Qiymət təxminidir."]}""");

        Assert.Equal("Brend: Ferrari", result.Notes[0]);
        Assert.Equal("Qiymət təxminidir.", result.Notes[1]);
        Assert.Contains("Ferrari", result.Notes[2]);
    }

    [Fact]
    public void IgnoresBlankAndNonStringNotes()
    {
        var result = Read("""{"notes":["  ", 7, "Brend: Ford"]}""");

        Assert.Equal(["Brend: Ford"], result.Notes);
    }

    [Fact]
    public void RejectsAStringWhereANumberWasExpected()
    {
        var result = Read("""{"priceMax":"otuz min"}""");

        Assert.Null(result.Filter.PriceMax);
    }
}
