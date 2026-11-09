namespace Entry.Services;

/// <summary>
/// The English words that name a value the listings store in Azerbaijani.
/// </summary>
/// <remarks>
/// <para>
/// The values themselves come from the data, but the bridge between the two
/// languages does not exist in the data and has to be stated. Buyers type
/// "diesel" as readily as "dizel", and without this the word survives as free
/// text and the search looks for a string no listing contains.
/// </para>
/// <para>
/// Shared by both halves of the pipeline so they cannot disagree: the rule
/// parser uses it to recognise a value, and <see cref="AiFilterReader"/> uses
/// it to confirm that a value the model chose was really named. Only
/// equivalents belong here — never a near-enough substitute, which is the very
/// thing the reader's check exists to reject.
/// </para>
/// </remarks>
public static class SearchSynonyms
{
    private static readonly Dictionary<string, string[]> ByValue = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Benzin"] = ["petrol", "gasoline", "gas"],
        ["Dizel"] = ["diesel"],
        ["Hibrid"] = ["hybrid"],
        ["Elektrik"] = ["electric", "ev"],
        ["Avtomat"] = ["automatic", "auto", "avtomatik"],
        ["Mexaniki"] = ["manual", "stick", "mexanika"],
        ["Hetçbek"] = ["hatchback", "hatch", "hetcbek"],
        ["Sedan"] = ["saloon"],
        ["SUV"] = ["crossover", "krossover", "jeep", "cip"],
        ["Kupe"] = ["coupe"],
        ["Universal"] = ["wagon", "estate"],
        ["Miniven"] = ["minivan", "van"],
        ["Pikap"] = ["pickup", "pick-up", "truck"],
    };

    /// <summary>
    /// Other spellings of <paramref name="value"/>, or empty when it has none.
    /// </summary>
    public static IReadOnlyList<string> For(string value) =>
        ByValue.TryGetValue(value, out var equivalents) ? equivalents : [];
}
