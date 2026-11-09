using Entry.Services;

namespace CarListing.Tests;

/// <summary>
/// A 7B model rarely returns the bare object it was asked for, so what the
/// service salvages from the answer decides whether a search works.
/// </summary>
public class OllamaJsonExtractionTests
{
    [Fact]
    public void KeepsACleanObjectUnchanged()
    {
        var json = """{"brand":"BMW","priceMax":20000}""";

        Assert.Equal(json, OllamaAiSearchParserService.ExtractJson(json));
    }

    [Fact]
    public void StripsAMarkdownFence()
    {
        var raw = """
            ```json
            {"brand":"BMW"}
            ```
            """;

        Assert.Equal("""{"brand":"BMW"}""", OllamaAiSearchParserService.ExtractJson(raw));
    }

    [Fact]
    public void DropsAnIntroSentence()
    {
        var raw = """Here is the filter you asked for: {"brand":"Toyota"}""";

        Assert.Equal("""{"brand":"Toyota"}""", OllamaAiSearchParserService.ExtractJson(raw));
    }

    [Fact]
    public void DropsARemarkAfterTheObject()
    {
        var raw = """{"brand":"Ford"} Let me know if you need anything else!""";

        Assert.Equal("""{"brand":"Ford"}""", OllamaAiSearchParserService.ExtractJson(raw));
    }

    [Fact]
    public void KeepsNestedBracesIntact()
    {
        // Taking the last brace, not the first closing one, is what makes a
        // nested object survive.
        var raw = """Sure: {"filter":{"brand":"Kia"},"notes":[]} done""";

        Assert.Equal("""{"filter":{"brand":"Kia"},"notes":[]}""", OllamaAiSearchParserService.ExtractJson(raw));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("I could not understand that request.")]
    [InlineData("{ unterminated")]
    public void ReturnsNullWhenThereIsNoObjectToRead(string raw)
    {
        // Null is the caller's signal to fall back rather than to throw.
        Assert.Null(OllamaAiSearchParserService.ExtractJson(raw));
    }
}
