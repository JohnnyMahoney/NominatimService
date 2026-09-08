using GeocoderSolution.Data;
using GeocoderSolution.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GeocoderSolution.Tests;

public sealed class GeocodingCacheStoreTests
{
    [Fact]
    public async Task SavedResultCanBeReadAfterDbContextIsRecreated()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"geocoding-cache-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath};Pooling=False";

        try
        {
            await using (var writeContext = CreateContext(connectionString))
            {
                await writeContext.Database.MigrateAsync();
                var writeStore = CreateStore(writeContext);

                await writeStore.SaveAsync(
                    "address:453 WEST 12TH AVENUE, VANCOUVER, BC",
                    new NominatimPlace(
                        49.2601,
                        -123.1139,
                        "Vancouver City Hall",
                        "Vancouver City Hall, Vancouver, British Columbia, Canada"),
                    "normalizedAddress",
                    CancellationToken.None);
            }

            await using (var readContext = CreateContext(connectionString))
            {
                var readStore = CreateStore(readContext);

                var cached = await readStore.GetAsync(
                    "address:453 WEST 12TH AVENUE, VANCOUVER, BC",
                    CancellationToken.None);

                Assert.NotNull(cached);
                Assert.Equal(49.2601, cached.Latitude);
                Assert.Equal(-123.1139, cached.Longitude);
                Assert.Equal("Vancouver City Hall", cached.Name);
                Assert.Equal("normalizedAddress", cached.Strategy);
                Assert.NotEqual(default, cached.CreatedAt);
            }
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Theory]
    [InlineData("V5Z 1M2", "postal:V5Z1M2")]
    [InlineData("v5z1m2", "postal:V5Z1M2")]
    public void ForPostalCode_CreatesCanonicalNamespacedKey(
        string postalCode,
        string expected)
    {
        Assert.Equal(expected, GeocodingCacheKeys.ForPostalCode(postalCode));
    }

    private static GeocodingCacheDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GeocodingCacheDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new GeocodingCacheDbContext(options);
    }

    private static GeocodingCacheStore CreateStore(
        GeocodingCacheDbContext dbContext)
    {
        return new GeocodingCacheStore(
            dbContext,
            NullLogger<GeocodingCacheStore>.Instance);
    }
}
