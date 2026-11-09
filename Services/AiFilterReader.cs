using System.Text.Json;
using Entry.Dto;

namespace Entry.Services;

/// <summary>
/// Turns the model's JSON answer into a filter, rejecting anything it cannot
/// justify.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="GeminiAiSearchParserService"/> and free of I/O, so
/// the rules that decide what a user's search actually does are testable without
/// a network call or an API key.
/// </para>
/// <para>
/// The model's answer is a suggestion. Values must exist in the listings, prices
/// and years must be plausible, and a reversed range is repaired rather than
/// passed through to a query that would match nothing.
/// </para>
/// </remarks>
public static class AiFilterReader
{
    public static AiSearchResultDto Read(string query, string json, SearchVocabulary vocabulary)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var notes = new List<string>();

        var filter = new CarSearchFilterDto
        {
            // Words that became no other field still narrow the results, but
            // only if they are the user's own words - see FreeText.
            Search = FreeText(root, "search", query),
            Brand = FromVocabulary(root, "brand", vocabulary.Brands, notes, "marka"),
            Model = FromVocabulary(root, "model", vocabulary.Models, notes, "model"),
            City = FromVocabulary(root, "city", vocabulary.Cities, notes, "şəhər"),
            FuelType = Grounded(root, "fuelType", vocabulary.FuelTypes, notes, "yanacaq növü", query),
            BodyType = Grounded(root, "bodyType", vocabulary.BodyTypes, notes, "ban növü", query),
            Transmission = Grounded(root, "transmission", vocabulary.Transmissions, notes, "sürətlər qutusu", query),
            PriceMin = Money(root, "priceMin"),
            PriceMax = Money(root, "priceMax"),
            YearMin = Year(root, "yearMin"),
            YearMax = Year(root, "yearMax"),
            MileageMax = Whole(root, "mileageMax", 0, 2_000_000),
            SortBy = OneOf(root, "sortBy", ["newest", "price", "year", "mileage"]),
            SortDirection = OneOf(root, "sortDirection", ["asc", "desc"]),
        };

        // A reversed range matches nothing, and the user meant a range either
        // way round.
        if (filter.PriceMin > filter.PriceMax)
            (filter.PriceMin, filter.PriceMax) = (filter.PriceMax, filter.PriceMin);
        if (filter.YearMin > filter.YearMax)
            (filter.YearMin, filter.YearMax) = (filter.YearMax, filter.YearMin);

        // A direction with nothing to sort by does nothing; a sort field with no
        // direction gets the one people expect - cheapest first for price,
        // newest first for everything else.
        if (filter.SortBy is null)
            filter.SortDirection = null;
        else
            filter.SortDirection ??= filter.SortBy == "price" ? "asc" : "desc";

        // A filter cannot exclude, so anything the sentence rejected has to be
        // removed rather than searched for.
        DropRejectedTerms(filter, query, notes);

        // The model's own remarks lead, because they explain the query as a
        // whole; rejected values follow them.
        notes.InsertRange(0, Lines(root, "notes"));

        return new AiSearchResultDto { Query = query, Filter = filter, Notes = notes };
    }

    /// <summary>
    /// Words that mark what follows them as unwanted.
    /// </summary>
    private static readonly string[] NegationMarkers =
    [
        "not", "no", "don't", "dont", "do not", "doesn't", "without", "except",
        "besides", "other than", "olmasın", "olmasin", "istəmirəm", "istemirem",
        "başqa", "basqa", "xaric",
    ];

    /// <summary>
    /// Strips values the sentence asked to avoid.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The filter has no "exclude" side: every field it sets narrows the
    /// results to that value. So a sentence like "I want a car but I do not
    /// want BMW" cannot be honoured, only misread — and a model that puts
    /// "bmw" into <c>brand</c> or <c>search</c> returns precisely the listings
    /// the user ruled out. Returning nothing for that term is the honest
    /// answer; returning the opposite is the worst one.
    /// </para>
    /// <para>
    /// The prompt already forbids this, but a 7B model follows it only most of
    /// the time, and "most of the time" is not good enough for a rule that
    /// inverts intent when it fails. So the sentence is checked here too: any
    /// term that appears after a negation word is dropped from whichever field
    /// carries it.
    /// </para>
    /// </remarks>
    private static void DropRejectedTerms(
        CarSearchFilterDto filter,
        string query,
        List<string> notes)
    {
        if (!TryFindNegation(query, out var rejectedPart)) return;

        void Drop(ref string? field, string labelAz)
        {
            if (field is null) return;
            if (!rejectedPart.Contains(field, StringComparison.OrdinalIgnoreCase)) return;

            notes.Add($"Sorğuda «{field}» istənilmədiyi bildirildi, ona görə {labelAz} filtri qoyulmadı.");
            field = null;
        }

        var brand = filter.Brand;
        var model = filter.Model;
        var city = filter.City;
        var fuel = filter.FuelType;
        var body = filter.BodyType;
        var transmission = filter.Transmission;

        Drop(ref brand, "marka");
        Drop(ref model, "model");
        Drop(ref city, "şəhər");
        Drop(ref fuel, "yanacaq növü");
        Drop(ref body, "ban növü");
        Drop(ref transmission, "sürətlər qutusu");

        filter.Brand = brand;
        filter.Model = model;
        filter.City = city;
        filter.FuelType = fuel;
        filter.BodyType = body;
        filter.Transmission = transmission;

        // Free text is the likeliest place a rejected word survives, because
        // it accepts anything the user actually typed - including the word
        // they typed in order to rule it out.
        if (filter.Search is not null)
        {
            var kept = filter.Search
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(term => !rejectedPart.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            filter.Search = kept.Length == 0 ? null : string.Join(' ', kept);
        }
    }

    /// <summary>
    /// The part of the sentence a negation applies to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two languages put the negation on opposite sides of what it
    /// rejects. English leads with it — "not a BMW" — so the rejected words
    /// follow. Azerbaijani trails it — "SUV olmasın" — so they precede. Taking
    /// the whole sentence for a trailing marker, and the tail for a leading
    /// one, covers both.
    /// </para>
    /// <para>
    /// It is deliberately crude: "not a BMW, a diesel" loses the diesel too.
    /// That errs towards dropping a filter rather than towards searching for
    /// the opposite of what was asked, and a dropped filter is visible in the
    /// notes while an inverted one is not.
    /// </para>
    /// </remarks>
    private static bool TryFindNegation(string query, out string rejectedPart)
    {
        // Markers that come after what they reject; the rest come before.
        string[] trailing =
            ["olmasın", "olmasin", "istəmirəm", "istemirem", "xaric"];

        var earliestLeading = -1;
        var trailingAt = -1;

        foreach (var marker in NegationMarkers)
        {
            var at = query.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (at < 0) continue;

            // Only a whole word counts: "another" must not read as "not".
            var before = at == 0 ? ' ' : query[at - 1];
            var afterIndex = at + marker.Length;
            var after = afterIndex >= query.Length ? ' ' : query[afterIndex];
            if (char.IsLetter(before) || char.IsLetter(after)) continue;

            if (trailing.Contains(marker, StringComparer.OrdinalIgnoreCase))
            {
                if (trailingAt < 0 || at < trailingAt) trailingAt = at;
            }
            else if (earliestLeading < 0 || at < earliestLeading)
            {
                earliestLeading = at;
            }
        }

        // A trailing marker rejects what came before it, up to the previous
        // clause: "SUV olmasın, sedan istəyirəm" rules out the SUV and still
        // asks for the sedan, so only the text before the marker is suspect,
        // and only back as far as the last comma.
        if (trailingAt >= 0)
        {
            var clauseStart = query.LastIndexOfAny([',', ';', '.'], Math.Max(trailingAt - 1, 0));
            rejectedPart = query[(clauseStart + 1)..trailingAt];

            // A leading marker later in the sentence rejects its own tail too.
            if (earliestLeading > trailingAt)
                rejectedPart += " " + query[earliestLeading..];

            return true;
        }

        if (earliestLeading < 0)
        {
            rejectedPart = "";
            return false;
        }

        rejectedPart = query[earliestLeading..];
        return true;
    }

    /// <summary>
    /// Free-text search terms, accepted only when they came from the user.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A small model asked to fill <c>search</c> will sometimes translate the
    /// leftover words instead of copying them: "red" came back as "qızıl".
    /// A translation searches for a word the user never typed and that no
    /// listing need contain, so it narrows the results to nothing while looking
    /// like it worked.
    /// </para>
    /// <para>
    /// Every whitespace-separated term is therefore checked against the
    /// original sentence, and only the ones actually present survive. This also
    /// drops a term the model lifted out of its own instructions.
    /// </para>
    /// </remarks>
    private static string? FreeText(JsonElement root, string name, string query)
    {
        var value = Text(root, name);
        if (value is null) return null;

        var kept = value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => query.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return kept.Length == 0 ? null : string.Join(' ', kept);
    }

    /// <summary>
    /// The model's own notes, in order, skipping blanks.
    /// </summary>
    private static List<string> Lines(JsonElement root, string name)
    {
        var lines = new List<string>();
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array)
            return lines;

        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            var value = item.GetString()?.Trim();
            if (!string.IsNullOrEmpty(value)) lines.Add(value);
        }

        return lines;
    }

    private static string? Text(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
            return null;
        var value = el.GetString()?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// Accepts a value only if the listings actually contain it.
    /// </summary>
    /// <remarks>
    /// Matched case-insensitively and returned in the data's own casing, so a
    /// model writing "bakı" still matches the stored "Bakı". A rejected value
    /// becomes a note rather than vanishing silently: the buyer should know a
    /// word was ignored, or the result count will look inexplicable.
    /// </remarks>
    /// <summary>
    /// A vocabulary value, accepted only when the sentence supports it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="FromVocabulary"/> rejects a value the listings do not have,
    /// but it cannot see that a listed value is the wrong one. Asked for a
    /// "convertible" — a body this site does not sell — a small model will not
    /// answer null; it picks the nearest entry on the list and returns
    /// "Hetçbek". The filter then looks confident and quietly searches for
    /// cars the user did not ask for, which is worse than returning nothing
    /// for that field.
    /// </para>
    /// <para>
    /// So the value has to be traceable to the sentence: either the user wrote
    /// it, or they wrote a word that starts the same way. A prefix is enough to
    /// carry Azerbaijani suffixes and the English forms people mix in
    /// ("sedan" for "Sedan", "avtomatik" for "Avtomat"), while still rejecting
    /// a word with no overlap at all. A guess that fails this is dropped with a
    /// note, because a buyer who sees fewer results deserves to know a word was
    /// ignored.
    /// </para>
    /// </remarks>
    private static string? Grounded(
        JsonElement root,
        string name,
        IReadOnlyList<string> allowed,
        List<string> notes,
        string labelAz,
        string query)
    {
        var value = FromVocabulary(root, name, allowed, notes, labelAz);
        if (value is null) return null;

        if (IsSupportedBy(query, value)) return value;

        notes.Add($"Sorğuda {labelAz} göstərilmədiyi üçün «{value}» nəzərə alınmadı.");
        return null;
    }

    /// <summary>
    /// True when a word in the sentence actually names this value.
    /// </summary>
    /// <remarks>
    /// Three ways a sentence can name a value: it contains the value itself, it
    /// contains a word that starts with the value (so Azerbaijani suffixes
    /// survive — "sedanı", "SUV-lar"), or it contains a known English
    /// equivalent. "convertible" is none of these for any listed body, which is
    /// exactly the guess this rejects.
    /// </remarks>
    private static bool IsSupportedBy(string query, string value)
    {
        if (query.Contains(value, StringComparison.OrdinalIgnoreCase)) return true;

        var words = query.Split(
            [' ', ',', '.', ';', ':', '!', '?', '(', ')', '/', '"'],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
            if (word.StartsWith(value, StringComparison.OrdinalIgnoreCase))
                return true;

        var equivalents = SearchSynonyms.For(value);
        if (equivalents.Count == 0) return false;

        foreach (var word in words)
            foreach (var equivalent in equivalents)
                if (word.StartsWith(equivalent, StringComparison.OrdinalIgnoreCase))
                    return true;

        return false;
    }

    private static string? FromVocabulary(
        JsonElement root,
        string name,
        IReadOnlyList<string> allowed,
        List<string> notes,
        string labelAz)
    {
        var value = Text(root, name);
        if (value is null) return null;

        foreach (var candidate in allowed)
            if (string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase))
                return candidate;

        notes.Add($"«{value}» {labelAz} kimi tanınmadı və nəzərə alınmadı.");
        return null;
    }

    private static decimal? Money(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el)
            || el.ValueKind != JsonValueKind.Number
            || !el.TryGetDecimal(out var value))
            return null;

        return value is > 0 and <= 100_000_000 ? decimal.Round(value, 2) : null;
    }

    /// <summary>
    /// A year outside this range is a misread number, not a car. Next year is
    /// allowed: sellers list the coming model year.
    /// </summary>
    private static int? Year(JsonElement root, string name) =>
        Whole(root, name, 1900, DateTime.UtcNow.Year + 1);

    private static int? Whole(JsonElement root, string name, int min, int max)
    {
        if (!root.TryGetProperty(name, out var el)
            || el.ValueKind != JsonValueKind.Number
            || !el.TryGetInt32(out var value))
            return null;

        return value >= min && value <= max ? value : null;
    }

    private static string? OneOf(JsonElement root, string name, string[] allowed)
    {
        var value = Text(root, name);
        if (value is null) return null;

        foreach (var candidate in allowed)
            if (string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase))
                return candidate;

        return null;
    }
}
