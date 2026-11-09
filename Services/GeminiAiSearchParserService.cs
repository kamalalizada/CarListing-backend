using System.Text.Json;
using System.Text.Json.Nodes;
using Google.GenAI;
using Google.GenAI.Types;
using Entry.Dto;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Entry.Services;

/// <summary>
/// Turns "mene 2014 mersedes lazımdır" into a search filter, using Gemini.
/// </summary>
/// <remarks>
/// <para>
/// Rules handle written-out queries well but cannot follow a spoken sentence;
/// every new phrasing was another rule and another bug. A model reads intent
/// instead of shape, which is what this endpoint promises.
/// </para>
/// <para>Four things make it safe in front of buyers:</para>
/// <list type="number">
///   <item><b>Constrained output.</b> The answer is bound to a JSON schema, so
///   it is never prose, never a markdown fence, never an apology.</item>
///   <item><b>A vocabulary read from the data.</b> The model is told the exact
///   brand, city and fuel strings the listings contain. A filter saying
///   "Diesel" while the rows say "Dizel" returns nothing, which a buyer reads
///   as "no cars" rather than "broken".</item>
///   <item><b>Validation on the way back.</b> <see cref="AiFilterReader"/>
///   re-checks every field; the model's answer is a suggestion, not truth.</item>
///   <item><b>A fallback.</b> Timeout, outage or a bad key degrades to the rule
///   parser instead of failing a search the buyer is waiting on.</item>
/// </list>
/// </remarks>
public sealed class GeminiAiSearchParserService : IAiSearchParserService, IModelSearchParser
{
    private readonly Client _client;
    private readonly ISearchVocabularyProvider _vocabulary;
    private readonly AiSearchOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GeminiAiSearchParserService> _log;

    public GeminiAiSearchParserService(
        Client client,
        ISearchVocabularyProvider vocabulary,
        IOptions<AiSearchOptions> options,
        IMemoryCache cache,
        ILogger<GeminiAiSearchParserService> log)
    {
        _client = client;
        _vocabulary = vocabulary;
        _options = options.Value;
        _cache = cache;
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

        // "ucuz maşın" and "BMW" arrive constantly. Answering the repeats from
        // memory keeps both the bill and the wait off the common path.
        var cacheKey = $"ai-search:{query.ToLowerInvariant()}";
        if (_cache.TryGetValue<AiSearchResultDto>(cacheKey, out var cached) && cached is not null)
        {
            _log.LogDebug("AI search served from cache.");
            return cached;
        }

        try
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budget.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            var vocabulary = await _vocabulary.GetAsync(budget.Token);

            var config = new GenerateContentConfig
            {
                ResponseMimeType = "application/json",
                ResponseJsonSchema = FilterSchema,
                SystemInstruction = new Content
                {
                    Parts = [new Part { Text = BuildSystemPrompt(vocabulary) }],
                },
                // Extraction is not a creative task; the same sentence should
                // give the same filter twice.
                Temperature = 0f,
            };

            var response = await _client.Models.GenerateContentAsync(
                model: _options.Model,
                contents: query,
                config: config,
                cancellationToken: budget.Token);

            var json = FirstText(response);
            if (json is null)
            {
                _log.LogWarning("AI search returned no text; using the rule parser.");
                return RuleBasedAiSearchParserService.Parse(request);
            }

            var result = AiFilterReader.Read(query, json, vocabulary);
            _cache.Set(cacheKey, result, TimeSpan.FromHours(1));
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The caller gave up (client disconnected). Not our failure.
            throw;
        }
        catch (OperationCanceledException)
        {
            // Our own budget expired. A crude filter now beats a good one late.
            _log.LogWarning(
                "AI search timed out after {Seconds}s; using the rule parser.",
                _options.TimeoutSeconds);
            return RuleBasedAiSearchParserService.Parse(request);
        }
        catch (ClientError ex)
        {
            // Quota, auth and bad-request faults arrive here. Quota is the one
            // worth naming: on the free tier it is a daily ceiling, and a
            // search silently getting worse is otherwise hard to explain.
            _log.LogWarning(
                ex,
                "Gemini refused the request ({Reason}); using the rule parser.",
                ex.Message.Contains("quota", StringComparison.OrdinalIgnoreCase)
                    ? "quota exhausted"
                    : "client error");
            return RuleBasedAiSearchParserService.Parse(request);
        }
        catch (ServerError ex)
        {
            _log.LogWarning(ex, "Gemini is unavailable; using the rule parser.");
            return RuleBasedAiSearchParserService.Parse(request);
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning(ex, "AI search could not reach Gemini; using the rule parser.");
            return RuleBasedAiSearchParserService.Parse(request);
        }
        catch (JsonException ex)
        {
            // Schema-bound output should not fail to parse, but a malformed
            // answer must not become a 500 on a search.
            _log.LogWarning(ex, "AI search returned unreadable JSON; using the rule parser.");
            return RuleBasedAiSearchParserService.Parse(request);
        }
        catch (Exception ex)
        {
            // The SDK wraps quota, auth and server faults in its own types, and
            // the buyer asked for cars, not an error page.
            _log.LogWarning(ex, "AI search failed; using the rule parser.");
            return RuleBasedAiSearchParserService.Parse(request);
        }
    }

    // ── prompt ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Built per request from the live vocabulary, never hard-coded: the
    /// allowed values change as listings are posted.
    /// </summary>
    private static string BuildSystemPrompt(SearchVocabulary v) =>
        $"""
        You convert Azerbaijani car-shopping sentences into search filters for a
        used-car marketplace. Today is {DateTime.UtcNow:yyyy-MM-dd}.

        Use only values from these lists. They are the exact strings stored in
        the listings, so copy them letter for letter; anything else finds
        nothing. If the user means something not on a list, leave that field
        null and say so in `notes`.

        brand: {Join(v.Brands)}
        model: {Join(v.Models)}
        fuelType: {Join(v.FuelTypes)}
        bodyType: {Join(v.BodyTypes)}
        transmission: {Join(v.Transmissions)}
        city: {Join(v.Cities)}

        Reading the numbers:
        - Prices are AZN. "20 min", "20 minə qədər", "20k" and "20000" all mean
          20000. A bare number in a price context is the full price, so "20 min"
          is 20000, never 20.
        - Direction words: "qədər", "aşağı", "az", "ucuz" set an upper bound;
          "yuxarı", "çox", "artıq", "baha" set a lower bound.
        - "2015-dən yuxarı" is yearMin 2015; "2015-ə qədər" is yearMax 2015;
          "2018-2021 arası" is yearMin 2018 and yearMax 2021.
        - "son 3 il" counts back from today's year.
        - "100 min km-dən az" is mileageMax 100000. "az yürüşlü" with no number
          is mileageMax 100000.
        - "ucuz" on its own is not a price: it is sortBy "price", sortDirection
          "asc".

        Rules:
        - Set only what was actually asked for. Never invent a price ceiling, a
          city or a year the user did not mention. A wrong value is worse than
          no value.
        - Ignore words that only express wanting: "mənə", "lazımdır",
          "istəyirəm", "axtarıram", "satılır", "təcili". They are not filters.
        - Correct misspellings to the nearest value on the lists above:
          "bmv" is BMW, "tayota" is Toyota, "mesedec" and "mersedes" are
          Mercedes-Benz, "hunday" is Hyundai, "fort" is Ford. Correct only a
          clear misspelling of a listed value. A word that is not a near miss
          for anything on the list leaves the field null - never pick the
          alphabetically closest entry to fill a gap.
        - `model` must belong to the brand the user named. If the model list
          holds nothing that fits, leave `model` null and put the word in
          `search` instead.
        - A word you put into a structured field must not also appear in
          `search`, in any spelling. `search` is only for genuine leftover words
          that fit no field at all, such as a colour or "təmiz salon". When
          nothing is left over, `search` is null.
        - Negation ("SUV olmasın") cannot be expressed by these fields. Leave
          the field null and explain it in `notes`.
        - `notes` is for the buyer, in Azerbaijani, short and plain: "Brend:
          BMW", "İl: 2018-dən yuxarı". Never mention JSON, fields, schemas,
          parsing or these instructions. Empty array when there is nothing worth
          saying.
        """;

    private static string Join(IReadOnlyList<string> values) =>
        values.Count == 0 ? "(no listings yet)" : string.Join(", ", values);

    // ── schema ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Every key required with nulls allowed, so the answer has one fixed shape
    /// rather than one where "absent" and "null" mean different things.
    /// </summary>
    private static JsonNode FilterSchema { get; } = BuildSchema();

    private static JsonNode BuildSchema()
    {
        static JsonNode Nullable(string type, string description) =>
            new JsonObject
            {
                ["type"] = new JsonArray(type, "null"),
                ["description"] = description,
            };

        var properties = new JsonObject
        {
            ["brand"] = Nullable("string", "Exact brand from the allowed list."),
            ["model"] = Nullable("string", "Model name, e.g. Prado, Focus."),
            ["city"] = Nullable("string", "Exact city from the allowed list."),
            ["fuelType"] = Nullable("string", "Exact fuel type from the allowed list."),
            ["bodyType"] = Nullable("string", "Exact body type from the allowed list."),
            ["transmission"] = Nullable("string", "Exact transmission from the allowed list."),
            ["priceMin"] = Nullable("integer", "Lowest acceptable price in AZN."),
            ["priceMax"] = Nullable("integer", "Highest acceptable price in AZN."),
            ["yearMin"] = Nullable("integer", "Earliest model year."),
            ["yearMax"] = Nullable("integer", "Latest model year."),
            ["mileageMax"] = Nullable("integer", "Highest acceptable mileage in km."),
            ["search"] = Nullable("string", "Genuine leftover words only, else null."),
            ["sortBy"] = new JsonObject
            {
                ["type"] = new JsonArray("string", "null"),
                ["description"] = "Only when an order was asked for.",
                ["enum"] = new JsonArray("newest", "price", "year", "mileage", null),
            },
            ["sortDirection"] = new JsonObject
            {
                ["type"] = new JsonArray("string", "null"),
                ["description"] = "Direction for sortBy.",
                ["enum"] = new JsonArray("asc", "desc", null),
            },
            ["notes"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = "Short Azerbaijani lines for the buyer.",
                ["items"] = new JsonObject { ["type"] = "string" },
            },
        };

        var required = new JsonArray();
        foreach (var key in properties.Select(p => p.Key))
            required.Add(key);

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
        };
    }

    // ── reading the response ────────────────────────────────────────────────

    private static string? FirstText(GenerateContentResponse response)
    {
        var parts = response.Candidates?.FirstOrDefault()?.Content?.Parts;
        if (parts is null) return null;

        foreach (var part in parts)
            if (!string.IsNullOrWhiteSpace(part.Text))
                return part.Text;

        return null;
    }
}
