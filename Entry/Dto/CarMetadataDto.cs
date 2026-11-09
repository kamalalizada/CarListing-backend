using System.Text.Json.Serialization;

namespace Entry.Dto;

/// <summary>
/// Everything a filter panel needs to build its dropdowns in one call.
/// </summary>
/// <remarks>
/// The value lists come from the listings themselves rather than from
/// constants. A dropdown offering "Coupe" when nobody has listed one sends the
/// buyer to an empty result page, and a hard-coded list silently goes stale the
/// first time someone posts a brand it does not know.
/// </remarks>
public class CarMetadataDto
{
    public List<string> Brands { get; set; } = new();

    /// <summary>Every model in the listings, for an unfiltered dropdown.</summary>
    public List<string> Models { get; set; } = new();

    /// <summary>
    /// Models grouped by the brand they belong to.
    /// </summary>
    /// <remarks>
    /// A flat model list puts Sonata under BMW. Sending the grouping with the
    /// rest of the metadata lets the panel narrow the second dropdown the
    /// moment a brand is picked, without a second round trip per selection.
    /// </remarks>
    public Dictionary<string, List<string>> ModelsByBrand { get; set; } = new();

    public List<string> Cities { get; set; } = new();
    public List<string> FuelTypes { get; set; } = new();
    public List<string> BodyTypes { get; set; } = new();
    public List<string> Transmissions { get; set; } = new();

    /// <summary>Values accepted by <c>sortBy</c> on the listing endpoint.</summary>
    public List<string> SortFields { get; set; } = new();

    /// <summary>
    /// The former name of <see cref="SortFields"/>.
    /// </summary>
    /// <remarks>
    /// Kept so a client written against the old contract keeps working. It
    /// serialises the same list and has no setter of its own, so the two can
    /// never disagree.
    /// </remarks>
    [JsonPropertyName("sortOptions")]
    public List<string> SortOptions => SortFields;
}
