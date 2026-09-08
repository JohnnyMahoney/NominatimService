using GeocoderSolution.DTOs;
using Microsoft.EntityFrameworkCore;

namespace GeocoderSolution.Data;

public sealed class GeocodingCacheStore(GeocodingCacheDbContext dbContext, ILogger<GeocodingCacheStore> logger)
{
    public async Task<GeocodingCacheEntry?> GetAsync(string cacheKey, CancellationToken cancellationToken)
    {
        var entry = await dbContext.GeocodingCacheEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.CacheKey == cacheKey,
                cancellationToken);

        if (entry is null)
        {
            logger.LogInformation("Geocoding cache miss for key {CacheKey}", cacheKey);
        }
        else
        {
            logger.LogInformation("Geocoding cache hit for key {CacheKey}", cacheKey);
        }

        return entry;
    }

    public async Task SaveAsync(string cacheKey, NominatimPlace place, string strategy, CancellationToken cancellationToken)
    {
        var createdAt = DateTimeOffset.UtcNow;

        var rowsAdded = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "GeocodingCacheEntries"
                ("CacheKey", "Latitude", "Longitude", "Name", "DisplayName", "Strategy", "CreatedAt")
            VALUES
                ({cacheKey}, {place.Latitude}, {place.Longitude}, {place.Name}, {place.DisplayName}, {strategy}, {createdAt})
            ON CONFLICT ("CacheKey") DO NOTHING;
            """, cancellationToken);

        if (rowsAdded > 0)
        {
            logger.LogInformation("Saved geocoding result for cache key {CacheKey}", cacheKey);
        }
    }
}
