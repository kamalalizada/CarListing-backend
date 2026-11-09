using System.Globalization;
using System.Text.RegularExpressions;
using Entry.Dto;

namespace Entry.Services;

/// <summary>
/// What a rule parse produced, and how much of the sentence it accounts for.
/// </summary>
/// <param name="Filter">The filter built from the parts that were understood.</param>
/// <param name="Matched">Fields the rules filled.</param>
/// <param name="Leftover">
/// Words that became no field. The caller uses this to decide whether the parse
/// is good enough to skip the model.
/// </param>
public sealed record RuleParse(
    CarSearchFilterDto Filter,
    IReadOnlyList<string> Matched,
    string Leftover);

/// <summary>
/// Reads the shapes people actually type, without a model call.
/// </summary>
/// <remarks>
/// <para>
/// Most searches on this site are one of a handful of phrasings - a brand, a
/// fuel, a budget, a year floor. Those are cheap to recognise and instant to
/// answer, so sending them to a language model buys nothing and costs money on
/// every keystroke a user commits.
/// </para>
/// <para>
/// The vocabulary comes from the listings rather than a constant, so a brand
/// nobody has listed is not matched and a brand someone just listed is - the
/// same rule the model half of the pipeline follows.
/// </para>
/// </remarks>
public static class QueryRuleParser
{
    public static RuleParse Parse(string query, SearchVocabulary vocabulary)
    {
        var filter = new CarSearchFilterDto();
        var matched = new List<string>();

        // Lower-cased for matching; `remaining` is carved up as pieces are
        // claimed, so whatever is left is genuinely unaccounted for.
        var text = Normalize(query);
        var remaining = text;

        // Vocabulary terms first: they are exact strings and the longest match
        // wins, so "Land Cruiser" is not shadowed by "Land".
        remaining = TakeVocabulary(remaining, vocabulary.Brands, v => filter.Brand = v, "brand", matched);
        remaining = TakeVocabulary(remaining, vocabulary.Cities, v => filter.City = v, "city", matched);
        remaining = TakeVocabulary(remaining, vocabulary.FuelTypes, v => filter.FuelType = v, "fuelType", matched);
        remaining = TakeVocabulary(remaining, vocabulary.BodyTypes, v => filter.BodyType = v, "bodyType", matched);
        remaining = TakeVocabulary(remaining, vocabulary.Transmissions, v => filter.Transmission = v, "transmission", matched);

        // Mileage before price: "100 min km" must not be read as 100000 AZN.
        remaining = TakeMileage(remaining, filter, matched);
        remaining = TakeYear(remaining, filter, matched);
        remaining = TakePrice(remaining, filter, matched);
        remaining = TakeSort(remaining, filter, matched);

        var leftover = Tidy(remaining);
        if (leftover.Length > 0) filter.Search = leftover;

        return new RuleParse(filter, matched, leftover);
    }

    /// <summary>
    /// Lower-cases and folds the Azerbaijani letters people commonly type in
    /// their ASCII form, so "gence" matches "Gəncə" and "hetcbek" matches
    /// "Hetçbek". Phones without an Azerbaijani keyboard produce these.
    /// </summary>
    private static string Normalize(string value)
    {
        var lowered = value.ToLower(CultureInfo.GetCultureInfo("az-Latn-AZ"));
        return lowered
            .Replace('ə', 'e').Replace('ı', 'i').Replace('ö', 'o')
            .Replace('ü', 'u').Replace('ğ', 'g').Replace('ş', 's')
            .Replace('ç', 'c');
    }

    private static string Tidy(string value) =>
        Regex.Replace(value, @"[\s,.;]+", " ").Trim();

    // ── vocabulary ──────────────────────────────────────────────────────────

    private static string TakeVocabulary(
        string text,
        IReadOnlyList<string> allowed,
        Action<string> assign,
        string fieldName,
        List<string> matched)
    {
        // Longest first: a two-word brand must win over a one-word prefix.
        foreach (var value in allowed.OrderByDescending(v => v.Length))
        {
            // The stored spelling, then the English ones a buyer may type
            // instead: "diesel" has to find the listings filed under "Dizel".
            foreach (var spelling in Spellings(value))
            {
                var needle = Normalize(spelling);
                if (needle.Length < 2) continue;

                var at = text.IndexOf(needle, StringComparison.Ordinal);
                if (at < 0) continue;

                // Whole words only, so "kia" does not fire inside "makiyaj".
                if (!MeasureMatch(text, at, needle.Length, out var span)) continue;

                // The value written into the filter is always the stored one,
                // whichever spelling matched; anything else finds no rows.
                assign(value);
                matched.Add(fieldName);

                // The suffix goes with the stem. Removing only "Gəncə" from
                // "Gəncədə" strands "də", which then rides along in Search and
                // narrows the results by a fragment the user never typed.
                return text.Remove(at, span);
            }
        }

        return text;
    }

    /// <summary>The stored spelling first, then its known equivalents.</summary>
    private static IEnumerable<string> Spellings(string value)
    {
        yield return value;
        foreach (var equivalent in SearchSynonyms.For(value))
            yield return equivalent;
    }

    /// <summary>
    /// Decides whether a hit is a real word match, and how much text it covers.
    /// </summary>
    /// <param name="span">
    /// The stem plus any case suffix, so the caller can remove the whole word.
    /// </param>
    /// <remarks>
    /// Azerbaijani inflects the noun people search with: "Gəncə" is typed
    /// "Gəncədə", "Bakı" becomes "Bakıda", "sedan" becomes "sedanlar".
    /// Requiring a clean word end would reject every one of them. The start is
    /// still anchored, so "kia" cannot fire inside "makiyaj".
    /// </remarks>
    private static bool MeasureMatch(string text, int at, int length, out int span)
    {
        span = length;

        if (at > 0 && char.IsLetterOrDigit(text[at - 1])) return false;

        var end = at + length;
        if (end >= text.Length) return true;

        // Only a short suffix may follow; a longer run is a different word.
        var suffix = 0;
        while (end + suffix < text.Length && char.IsLetter(text[end + suffix])) suffix++;
        if (suffix > 4) return false;

        span = length + suffix;
        return true;
    }

    // ── numbers ─────────────────────────────────────────────────────────────

    private static readonly RegexOptions Opts =
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    /// <summary>"100 min km-den az", "150000 km-e qeder".</summary>
    private static string TakeMileage(string text, CarSearchFilterDto filter, List<string> matched)
    {
        var m = Regex.Match(text, @"(\d{1,3})\s*min\s*km\w*(?:\s*[-–]?\s*(?:d[ae]n)?\s*(?:az|asagi|qeder|kimi))?|(\d{4,7})\s*km\w*(?:\s*[-–]?\s*(?:d[ae]n)?\s*(?:az|asagi|qeder|kimi))?", Opts);
        if (!m.Success) return text;

        var value = m.Groups[1].Success
            ? int.Parse(m.Groups[1].Value) * 1000
            : int.Parse(m.Groups[2].Value);

        filter.MileageMax = value;
        matched.Add("mileageMax");
        return text.Remove(m.Index, m.Length);
    }

    /// <summary>"2015-den yuxari", "2018+", "2016-2020", "2019-e qeder".</summary>
    private static string TakeYear(string text, CarSearchFilterDto filter, List<string> matched)
    {
        var range = Regex.Match(text, @"\b(19\d{2}|20\d{2})\s*[-–]\s*(19\d{2}|20\d{2})\b", Opts);
        if (range.Success)
        {
            filter.YearMin = int.Parse(range.Groups[1].Value);
            filter.YearMax = int.Parse(range.Groups[2].Value);
            matched.Add("yearMin");
            matched.Add("yearMax");
            return text.Remove(range.Index, range.Length);
        }

        var floor = Regex.Match(text, @"\b(19\d{2}|20\d{2})\s*[-–]?\s*(\+|d[ae]n yuxari|ve yuxari|sonra|ustu)", Opts);
        if (floor.Success)
        {
            filter.YearMin = int.Parse(floor.Groups[1].Value);
            matched.Add("yearMin");
            return text.Remove(floor.Index, floor.Length);
        }

        var ceiling = Regex.Match(text, @"\b(19\d{2}|20\d{2})\s*[-–]?\s*[ae]?\s*(qeder|kimi|d[ae]n asagi|ve asagi|evvel)", Opts);
        if (ceiling.Success)
        {
            filter.YearMax = int.Parse(ceiling.Groups[1].Value);
            matched.Add("yearMax");
            return text.Remove(ceiling.Index, ceiling.Length);
        }

        return text;
    }

    /// <summary>
    /// "30 mine qeder", "18-22 min", "25000 azn", and the English forms
    /// "under 20000", "up to 30k", "over 15000".
    /// </summary>
    private static string TakePrice(string text, CarSearchFilterDto filter, List<string> matched)
    {
        var range = Regex.Match(text, @"\b(\d{1,3})\s*[-–]\s*(\d{1,3})\s*min\b", Opts);
        if (range.Success)
        {
            filter.PriceMin = int.Parse(range.Groups[1].Value) * 1000;
            filter.PriceMax = int.Parse(range.Groups[2].Value) * 1000;
            matched.Add("priceMin");
            matched.Add("priceMax");
            return text.Remove(range.Index, range.Length);
        }

        var ceiling = Regex.Match(
            text,
            @"\b(\d{1,3})\s*min\w*\s*(?:manat\w*\s*|azn\w*\s*)?(?:qeder|kimi|asagi)|\b(\d{4,7})\s*(?:azn|manat)\w*\s*(?:qeder|kimi|asagi)",
            Opts);
        if (ceiling.Success)
        {
            filter.PriceMax = ceiling.Groups[1].Success
                ? int.Parse(ceiling.Groups[1].Value) * 1000
                : int.Parse(ceiling.Groups[2].Value);
            matched.Add("priceMax");
            return text.Remove(ceiling.Index, ceiling.Length);
        }

        var floor = Regex.Match(text, @"\b(\d{1,3})\s*min\w*\s*(?:manat\w*\s*|azn\s*)?(?:yuxari|artiq|bahali)", Opts);
        if (floor.Success)
        {
            filter.PriceMin = int.Parse(floor.Groups[1].Value) * 1000;
            matched.Add("priceMin");
            return text.Remove(floor.Index, floor.Length);
        }

        // The same bounds written in English. People type "bmw under 20000" as
        // readily as the Azerbaijani form, and without these the number is left
        // over as free text: the search then looks for the string "under 20000"
        // in the listings and finds nothing.
        var englishCeiling = Regex.Match(
            text,
            @"\b(?:under|below|up\s+to|less\s+than|max|maximum|cheaper\s+than)\s*(\d{1,3})\s*k\b"
            + @"|\b(?:under|below|up\s+to|less\s+than|max|maximum|cheaper\s+than)\s*(\d{3,7})\b",
            Opts);
        if (englishCeiling.Success)
        {
            filter.PriceMax = englishCeiling.Groups[1].Success
                ? int.Parse(englishCeiling.Groups[1].Value) * 1000
                : int.Parse(englishCeiling.Groups[2].Value);
            matched.Add("priceMax");
            return text.Remove(englishCeiling.Index, englishCeiling.Length);
        }

        var englishFloor = Regex.Match(
            text,
            @"\b(?:over|above|more\s+than|starting\s+at|minimum)\s*(\d{1,3})\s*k\b"
            + @"|\b(?:over|above|more\s+than|starting\s+at|minimum)\s*(\d{3,7})\b",
            Opts);
        if (englishFloor.Success)
        {
            filter.PriceMin = englishFloor.Groups[1].Success
                ? int.Parse(englishFloor.Groups[1].Value) * 1000
                : int.Parse(englishFloor.Groups[2].Value);
            matched.Add("priceMin");
            return text.Remove(englishFloor.Index, englishFloor.Length);
        }

        return text;
    }

    /// <summary>"ucuz", "en yeni", "az yuruslu".</summary>
    private static string TakeSort(string text, CarSearchFilterDto filter, List<string> matched)
    {
        // "ucuz" is not a budget - it says how to order, not where to cut.
        var cheap = Regex.Match(text, @"\b(ucuz|serfeli)\w*", Opts);
        if (cheap.Success)
        {
            filter.SortBy = "price";
            filter.SortDirection = "asc";
            matched.Add("sortBy");
            return text.Remove(cheap.Index, cheap.Length);
        }

        var newest = Regex.Match(text, @"\b(en yeni|yeni model|teze)\w*", Opts);
        if (newest.Success)
        {
            filter.SortBy = "year";
            filter.SortDirection = "desc";
            matched.Add("sortBy");
            return text.Remove(newest.Index, newest.Length);
        }

        var lowMileage = Regex.Match(text, @"\b(az yurus\w*|az islenmis)", Opts);
        if (lowMileage.Success)
        {
            filter.SortBy = "mileage";
            filter.SortDirection = "asc";
            matched.Add("sortBy");
            return text.Remove(lowMileage.Index, lowMileage.Length);
        }

        return text;
    }
}
