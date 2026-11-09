using Entry.Dto;
using Microsoft.Extensions.Options;

namespace Entry.Services;

/// <summary>
/// Answers with rules when it can, and only pays for a model when it cannot.
/// </summary>
/// <remarks>
/// <para>
/// Most searches here are ordinary - a brand, a fuel, a budget, a year floor.
/// Rules recognise those instantly and for nothing, so sending them to a model
/// buys no accuracy and bills on every search. The model earns its cost on the
/// sentences rules cannot take apart.
/// </para>
/// <para>
/// The handover is decided by <see cref="RuleParseVerdict"/>, which is kept
/// separate and pure so the cost/quality tradeoff is testable without a network
/// call. When the model is unavailable the rule parse is still returned, so a
/// hybrid search never fails where a rule-only search would have worked.
/// </para>
/// </remarks>
public sealed class HybridAiSearchParserService : IAiSearchParserService
{
    /// <summary>
    /// Null when no model is configured.
    /// </summary>
    /// <remarks>
    /// Optional rather than required so the free half of the pipeline still
    /// runs without one. Making the model mandatory would drop local and CI
    /// deployments back to a weaker parser than the one already written, which
    /// is a worse search for no reason.
    /// </remarks>
    private readonly IModelSearchParser? _model;

    private readonly ISearchVocabularyProvider _vocabulary;
    private readonly AiSearchOptions _options;
    private readonly ILogger<HybridAiSearchParserService> _log;

    public HybridAiSearchParserService(
        ISearchVocabularyProvider vocabulary,
        IOptions<AiSearchOptions> options,
        ILogger<HybridAiSearchParserService> log,
        IModelSearchParser? model = null)
    {
        _vocabulary = vocabulary;
        _options = options.Value;
        _log = log;
        _model = model;
    }

    public async Task<AiSearchResultDto> ParseAsync(
        AiSearchRequestDto request,
        CancellationToken ct = default)
    {
        var query = request.Query.Trim();
        var vocabulary = await _vocabulary.GetAsync(ct);
        var rules = QueryRuleParser.Parse(query, vocabulary);

        var needsModel = RuleParseVerdict.NeedsModel(rules, _options.RuleLeftoverWordLimit);

        if (needsModel && _model is not null)
            return await _model.ParseAsync(request, ct);

        // Either the rules were enough, or there is no model to escalate to.
        // The rule parse is still the best answer available, so it is returned
        // rather than failing a search the user is waiting on.
        _log.LogDebug(
            "AI search answered by rules ({Matched}); model {State}.",
            string.Join(",", rules.Matched),
            _model is null ? "not configured" : "not needed");

        return new AiSearchResultDto
        {
            Query = query,
            Filter = rules.Filter,
            Notes = [],
        };
    }
}

/// <summary>
/// Decides whether a rule parse is good enough to skip the model.
/// </summary>
public static class RuleParseVerdict
{
    /// <summary>
    /// True when the sentence deserves a model call.
    /// </summary>
    /// <remarks>
    /// <para>Two conditions send a query on:</para>
    /// <list type="bullet">
    ///   <item>The rules found nothing at all. Escalating costs a fraction of a
    ///   cent; returning nothing costs a user.</item>
    ///   <item>Words are left over that no rule claimed. Those words usually
    ///   carry the intent the rules missed, and the model is what this product
    ///   promises for them.</item>
    /// </list>
    /// <para>
    /// A couple of stray words are tolerated: "satilir", "tecili" and the like
    /// appear constantly and mean nothing to a filter, so they are not worth a
    /// round trip. Model names are the common case here - they go into
    /// <c>Search</c> and still narrow the results.
    /// </para>
    /// </remarks>
    public static bool NeedsModel(RuleParse parse, int leftoverWordLimit)
    {
        if (parse.Matched.Count == 0) return true;

        var leftoverWords = CountWords(parse.Leftover);
        return leftoverWords > leftoverWordLimit;
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
