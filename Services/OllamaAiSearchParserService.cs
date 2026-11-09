using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Entry.Dto;
using Microsoft.Extensions.Options;

namespace Entry.Services;

/// <summary>
/// Turns "I need a 2014 Mercedes" into a search filter using a local model.
/// </summary>
/// <remarks>
/// <para>
/// Ollama runs on the developer's own machine, so a search costs nothing and
/// leaves no quota to exhaust. The tradeoff is that a 7B model is looser than a
/// hosted one: it sometimes wraps its answer in a markdown fence, sometimes
/// adds a sentence before the JSON, and sometimes returns JSON that does not
/// close. All three are treated as ordinary outcomes here rather than faults —
/// see <see cref="ExtractJson"/> — and anything still unreadable falls back to
/// the rule parser.
/// </para>
/// <para>
/// The prompt is written in English because these models follow English
/// instructions markedly better than Azerbaijani ones, but the vocabulary it is
/// given and the notes it returns stay in the language of the site, so a buyer
/// never sees the difference.
/// </para>
/// </remarks>
public sealed class OllamaAiSearchParserService : IAiSearchParserService, IModelSearchParser
{
    private readonly HttpClient _http;
    private readonly ISearchVocabularyProvider _vocabulary;
    private readonly AiSearchOptions _options;
    private readonly ILogger<OllamaAiSearchParserService> _log;

    public OllamaAiSearchParserService(
        HttpClient http,
        ISearchVocabularyProvider vocabulary,
        IOptions<AiSearchOptions> options,
        ILogger<OllamaAiSearchParserService> log)
    {
        _http = http;
        _vocabulary = vocabulary;
        _options = options.Value;
        _log = log;
    }

    public async Task<AiSearchResultDto> ParseAsync(
        AiSearchRequestDto request,
        CancellationToken ct = default)
    {
        var query = request.Query.Trim();

        // A kill switch that does not need a redeploy.
        if (!_options.Enabled)
            return RuleBasedAiSearchParserService.Parse(request);

        SearchVocabulary vocabulary;
        string raw;

        try
        {
            vocabulary = await _vocabulary.GetAsync(ct);

            var response = await _http.PostAsJsonAsync(
                "/api/generate",
                new OllamaRequest(
                    Model: _options.OllamaModel,
                    Prompt: BuildPrompt(query, vocabulary),
                    Format: "json",
                    Stream: false,
                    Options: Sampling),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                // Ollama answers 404 when the model has never been pulled,
                // which is the likeliest first-run mistake and reads nothing
                // like "not found" to someone whose server is plainly running.
                var detail = response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? $"model '{_options.OllamaModel}' is not installed - run: ollama pull {_options.OllamaModel}"
                    : $"HTTP {(int)response.StatusCode}";

                _log.LogWarning("Ollama refused the request ({Detail}); using the rule parser.", detail);
                return RuleBasedAiSearchParserService.Parse(request);
            }

            var body = await response.Content.ReadFromJsonAsync<OllamaResponse>(ct);
            raw = body?.Response ?? "";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The caller gave up (client disconnected). Not our failure.
            throw;
        }
        catch (TaskCanceledException)
        {
            // HttpClient.Timeout. A local model on CPU can simply be too slow,
            // and a crude filter now beats a good one after the buyer has left.
            _log.LogWarning(
                "Ollama did not answer within {Seconds}s; using the rule parser.",
                _http.Timeout.TotalSeconds);
            return RuleBasedAiSearchParserService.Parse(request);
        }
        catch (HttpRequestException ex)
        {
            // Nothing is listening at this address. Expected whenever the
            // developer has not started Ollama, so it is a warning, not an error.
            _log.LogWarning(
                "Ollama is not running at {BaseUrl} ({Message}); using the rule parser.",
                _http.BaseAddress,
                ex.Message);
            return RuleBasedAiSearchParserService.Parse(request);
        }

        var json = ExtractJson(raw);
        if (json is null)
        {
            _log.LogWarning(
                "Ollama returned no JSON object; using the rule parser. Raw: {Raw}",
                Truncate(raw));
            return RuleBasedAiSearchParserService.Parse(request);
        }

        try
        {
            return AiFilterReader.Read(query, json, vocabulary);
        }
        catch (JsonException ex)
        {
            // A small model closing a brace in the wrong place is routine, not
            // a bug to surface as a 500 on a search.
            _log.LogWarning(
                ex,
                "Ollama returned malformed JSON; using the rule parser. Raw: {Raw}",
                Truncate(json));
            return RuleBasedAiSearchParserService.Parse(request);
        }
    }

    // ── reading a small model's answer ──────────────────────────────────────

    /// <summary>
    /// Pulls the JSON object out of whatever the model wrapped it in.
    /// </summary>
    /// <remarks>
    /// Even asked for <c>format: json</c>, 7B models prepend "Here is the
    /// filter:", fence the answer in <c>```json</c>, or append a closing
    /// remark. Taking the span from the first brace to the last is enough for
    /// every one of those and costs nothing when the answer is already clean.
    /// Returns null when there is no brace pair at all, which is the caller's
    /// signal to fall back.
    /// </remarks>
    public static string? ExtractJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start) return null;

        return raw[start..(end + 1)];
    }

    private static string Truncate(string text) =>
        text.Length <= 300 ? text : text[..300] + "…";

    // ── prompt ──────────────────────────────────────────────────────────────

    /// <summary>
    /// System instructions and the user's sentence in one string, because
    /// <c>/api/generate</c> takes a single prompt.
    /// </summary>
    /// <remarks>
    /// Written in English: these models follow English instructions much more
    /// reliably than Azerbaijani ones, and the instructions are never shown to
    /// anyone. The values it may choose from, and the notes it writes back,
    /// stay in the site's own language.
    /// </remarks>
    private static string BuildPrompt(string query, SearchVocabulary v) =>
        $"""
        You convert car-shopping sentences into search filters for a used-car
        marketplace. Today is {DateTime.UtcNow:yyyy-MM-dd}.

        Reply with one JSON object and nothing else. No explanation, no
        greeting, no markdown code fence, no text before or after the object.

        The object has exactly these keys:
        brand, model, city, fuelType, bodyType, transmission, priceMin,
        priceMax, yearMin, yearMax, mileageMax, sortBy, sortDirection, search,
        notes

        Every key must be present. Use null for anything the sentence does not
        state. `notes` is an array of strings and is [] when there is nothing to
        say.

        These are the only values the listings contain. Copy them exactly,
        letter for letter, including Azerbaijani characters. A value spelled any
        other way matches no cars:

        brand: {Join(v.Brands)}
        model: {Join(v.Models)}
        fuelType: {Join(v.FuelTypes)}
        bodyType: {Join(v.BodyTypes)}
        transmission: {Join(v.Transmissions)}
        city: {Join(v.Cities)}

        Numbers:
        - Prices are in AZN. "20k", "20 thousand", "20 min" and "20000" all mean
          20000. A bare number in a price context is the whole price, never 20.
        - "under", "below", "up to", "less than", "qədər" set priceMax.
        - "above", "over", "more than", "from", "yuxarı" set priceMin.
        - "2018 to 2021" and "between 2018 and 2021" are yearMin 2018 and
          yearMax 2021. "2015 and newer" is yearMin 2015. "up to 2015" is
          yearMax 2015.
        - "under 100000 km" is mileageMax 100000. "low mileage" with no number
          is mileageMax 100000.
        - "cheap" is not a price: it is sortBy "price" with sortDirection "asc".
        - sortBy is one of newest, price, year, mileage, or null.
          sortDirection is asc, desc, or null.

        Rules:
        - Fill only what the sentence actually states. Never invent a price
          ceiling, a city or a year. A wrong value is worse than null.
        - Judge this sentence alone. Do not carry over values from any example
          above or from an earlier sentence; the examples show formatting, not
          content. If this sentence names no year, both yearMin and yearMax are
          null, and the same holds for every other field.
        - sortBy and sortDirection stay null unless the sentence asks for an
          order ("cheapest", "newest", "lowest mileage").
        - Ignore words that only express wanting: "I need", "I want", "I am
          looking for", "show me", "mənə", "lazımdır", "axtarıram".
        - Fix clear misspellings to a value on the lists: "bmv" is BMW,
          "toyata" is Toyota, "mersedes" is Mercedes-Benz, "hunday" is Hyundai.
          Only correct a near miss. If a word matches nothing on a list, leave
          that field null rather than choosing the closest entry.
        - `model` must belong to the brand named. If no listed model fits, leave
          it null.
        - A word you put into a structured field must not also appear in
          `search`, in any spelling. `search` holds only genuine leftovers such
          as a colour. When nothing is left over, `search` is null.
        - Copy leftover words into `search` exactly as the user typed them.
          Never translate them. If the user wrote "red", `search` is "red", not
          "qırmızı".
        - A concept the lists do not cover is not a near miss for something they
          do cover. "convertible", "coupe" and "minivan" are not bodyType values
          here unless they appear on the list above; leave bodyType null and say
          so in `notes`. The same holds for every other field.
        - Vague quantities are not exact ones. "around 2019" and "about 2019"
          mean a range, so use yearMin 2018 and yearMax 2020, never yearMin and
          yearMax both 2019. "not too expensive" states no number at all: leave
          priceMax null.
        - Negation. These fields can only include, never exclude. When the
          sentence rejects something — "not", "don't want", "except",
          "without", "olmasın", "istəmirəm" — the rejected word must not appear
          anywhere in the answer: not in its own field, and not in `search`
          either. Leave every affected field null and say what was ignored in
          `notes`. "I want a car but I do not want BMW" has no brand and no
          search term; answering brand "BMW" or search "bmw" returns exactly
          the cars the user ruled out, which is worse than returning nothing.
        - Write `notes` in Azerbaijani, short and plain, for the buyer to read:
          "Brend: BMW", "Qiymət: 20000 AZN-ə qədər", "İl: 2018-2021 arası".
          Never mention JSON, fields, schemas, parsing or these instructions.

        Sentence: {query}

        JSON:
        """;

    private static string Join(IReadOnlyList<string> values) =>
        values.Count == 0 ? "(no listings yet)" : string.Join(", ", values);

    // ── wire format ─────────────────────────────────────────────────────────

    /// <summary>
    /// Sampling settings that make the same sentence give the same filter.
    /// </summary>
    /// <remarks>
    /// Ollama defaults to a temperature of 0.8, which is meant for writing, not
    /// for extraction. At that setting the same query answered twice produced
    /// two different filters — once with the sentence correctly split into
    /// fields, once with the whole sentence dumped into <c>search</c>, which
    /// matches no listing. Temperature 0 makes the choice greedy, and seeding
    /// it fixes the remaining tie-breaks, so a user who repeats a search gets
    /// the results they saw the first time.
    /// </remarks>
    private static readonly OllamaOptions Sampling = new(Temperature: 0, Seed: 42);

    private sealed record OllamaRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("format")] string Format,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("options")] OllamaOptions Options);

    private sealed record OllamaOptions(
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("seed")] int Seed);

    private sealed record OllamaResponse(
        [property: JsonPropertyName("response")] string? Response);
}
