using Entry.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Entry.Services;

/// <summary>
/// Reads the allowed filter values from the active listings.
/// </summary>
/// <remarks>
/// Cached for a few minutes: the vocabulary changes only when someone posts a
/// listing with a new brand or city, and every AI search would otherwise run
/// five DISTINCT queries before it even reaches the model.
/// </remarks>
public sealed class DbSearchVocabularyProvider : ISearchVocabularyProvider
{
    private const string CacheKey = "search-vocabulary";
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public DbSearchVocabularyProvider(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<SearchVocabulary> GetAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out SearchVocabulary? cached) && cached is not null)
            return cached;

        var vocabulary = new SearchVocabulary(
            Brands: await DistinctAsync(c => c.Brand, ct),
            FuelTypes: await DistinctAsync(c => c.FuelType, ct),
            Models: await DistinctAsync(c => c.Model, ct),
            BodyTypes: await DistinctAsync(c => c.BodyType, ct),
            Transmissions: await DistinctAsync(c => c.Transmission, ct),
            Cities: await DistinctAsync(c => c.City, ct));

        _cache.Set(CacheKey, vocabulary, CacheFor);
        return vocabulary;
    }

    /// <summary>
    /// Distinct non-empty values from active listings, alphabetically.
    /// </summary>
    /// <remarks>
    /// Sorted so the prompt prefix stays byte-identical between requests —
    /// an unstable order would silently break prompt caching.
    /// </remarks>
    private async Task<IReadOnlyList<string>> DistinctAsync(
        System.Linq.Expressions.Expression<Func<Entry.Concrete.Car, string>> field,
        CancellationToken ct)
    {
        return await _db.Cars
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(field)
            .Where(value => value != null && value != "")
            .Distinct()
            .OrderBy(value => value)
            .Take(200)
            .ToListAsync(ct);
    }
}
