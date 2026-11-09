using Entry.Dto;

namespace Entry.Services;

/// <summary>
/// The values a filter is actually allowed to contain.
/// </summary>
/// <remarks>
/// These are read from the listings themselves rather than hard-coded. The
/// hard-coded list in <c>GET /Cars/metadata</c> says "Diesel" and "Automatic"
/// while the seeded rows say "Dizel" and "Avtomat", so a filter built from the
/// hard-coded vocabulary matches nothing. Reading the live values keeps the
/// parser correct whatever language the data happens to be in.
/// </remarks>
public sealed record SearchVocabulary(
    IReadOnlyList<string> Brands,
    IReadOnlyList<string> Models,
    IReadOnlyList<string> FuelTypes,
    IReadOnlyList<string> BodyTypes,
    IReadOnlyList<string> Transmissions,
    IReadOnlyList<string> Cities);

public interface ISearchVocabularyProvider
{
    Task<SearchVocabulary> GetAsync(CancellationToken ct = default);
}
