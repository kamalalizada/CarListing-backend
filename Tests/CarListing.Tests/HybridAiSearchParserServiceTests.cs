using Entry.Dto;
using Entry.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CarListing.Tests;

/// <summary>
/// The hybrid has to work with and without a model configured.
/// </summary>
public class HybridAiSearchParserServiceTests
{
    private sealed class StubVocabulary : ISearchVocabularyProvider
    {
        public Task<SearchVocabulary> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(new SearchVocabulary(
                Brands: ["Toyota"],
                Models: ["Prado"],
                FuelTypes: ["Dizel"],
                BodyTypes: ["SUV"],
                Transmissions: ["Avtomat"],
                Cities: ["Bakı"]));
    }

    /// <summary>Built the way the container builds it when no key is present.</summary>
    private static HybridAiSearchParserService WithoutModel() =>
        new(new StubVocabulary(),
            Options.Create(new AiSearchOptions()),
            NullLogger<HybridAiSearchParserService>.Instance);

    [Fact]
    public async Task UsesTheRuleParserWhenNoModelIsConfigured()
    {
        // The regression this guards: an earlier wiring dropped the whole
        // hybrid when the key was missing, falling back to a parser that reads
        // less than the rules already do.
        var result = await WithoutModel().ParseAsync(new AiSearchRequestDto
        {
            Query = "2015-dən yuxarı dizel SUV, 30 minə qədər"
        });

        Assert.Equal("Dizel", result.Filter.FuelType);
        Assert.Equal("SUV", result.Filter.BodyType);
        Assert.Equal(2015, result.Filter.YearMin);
        Assert.Equal(30_000, result.Filter.PriceMax);
    }

    [Fact]
    public async Task StillAnswersWhenTheRulesUnderstandNothingAndNoModelExists()
    {
        // Nothing to escalate to, so the empty rule parse is the answer. A
        // search must not fail just because the model is absent.
        var result = await WithoutModel().ParseAsync(new AiSearchRequestDto
        {
            Query = "uşaq üçün rahat və etibarlı nəsə lazımdır"
        });

        Assert.NotNull(result.Filter);
        Assert.Equal("uşaq üçün rahat və etibarlı nəsə lazımdır", result.Query);
    }

    [Fact]
    public async Task DoesNotCarryDeveloperNotesIntoTheResponse()
    {
        // The parser this replaced emitted "Rule-based parser MVP işləyir..."
        // on every search - an internal note that reached real users.
        var result = await WithoutModel().ParseAsync(new AiSearchRequestDto
        {
            Query = "Bakıda avtomat Toyota"
        });

        Assert.Empty(result.Notes);
    }
}
