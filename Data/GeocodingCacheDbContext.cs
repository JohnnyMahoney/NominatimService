using Microsoft.EntityFrameworkCore;

namespace GeocoderSolution.Data;

public sealed class GeocodingCacheDbContext(
    DbContextOptions<GeocodingCacheDbContext> options) : DbContext(options)
{
    public DbSet<GeocodingCacheEntry> GeocodingCacheEntries =>
        Set<GeocodingCacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var cacheEntry = modelBuilder.Entity<GeocodingCacheEntry>();

        cacheEntry.ToTable("GeocodingCacheEntries");
        cacheEntry.HasKey(entry => entry.CacheKey);
        cacheEntry.Property(entry => entry.CacheKey).HasMaxLength(512);
        cacheEntry.Property(entry => entry.Strategy).HasMaxLength(32);
    }
}
