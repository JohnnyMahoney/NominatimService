namespace GeocoderSolution.Data;

public sealed class GeocodingCacheEntry
{
    public required string CacheKey { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public string? Name { get; init; }
    public string? DisplayName { get; init; }
    public required string Strategy { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
