using System.Net;
using System.Text;
using GeocoderSolution.Configuration;
using GeocoderSolution.Data;
using GeocoderSolution.DTOs;
using GeocoderSolution.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GeocoderSolution.Tests;

public sealed class GeocodingServiceFallbackTests
{
    private const string PlaceResponse = """
        [{
          "lat": "49.2601",
          "lon": "-123.1139",
          "name": "Vancouver",
          "display_name": "Vancouver, British Columbia, Canada"
        }]
        """;

    [Fact]
    public async Task GeocodeAsync_UsesPostalCodeFallback()
    {
        using var fixture = CreateFixture("[]", PlaceResponse);
        var request = CreateRequest("999999 Fake Road, Vancouver, BC V5Z 1M2");

        var response = await fixture.Service.GeocodeAsync(request, CancellationToken.None);

        var result = Assert.Single(response.Results);
        Assert.True(result.Found);
        Assert.Equal("found", result.Status);
        Assert.Equal("postalCode", result.Strategy);
        Assert.Equal(
            ["999999 Fake Road, Vancouver, BC V5Z 1M2", "V5Z 1M2"],
            fixture.Queries);
    }

    [Fact]
    public async Task GeocodeAsync_ExtractsPostalCodeWithoutSpace()
    {
        using var fixture = CreateFixture("[]", PlaceResponse);
        var request = CreateRequest("999999 Fake Road, Vancouver, BC V5Z1M2");

        var response = await fixture.Service.GeocodeAsync(request, CancellationToken.None);

        var result = Assert.Single(response.Results);
        Assert.True(result.Found);
        Assert.Equal("postalCode", result.Strategy);
        Assert.Equal("V5Z 1M2", fixture.Queries[1]);
    }

    [Fact]
    public async Task GeocodeAsync_SkipsFallbackWhenPostalCodeIsMissing()
    {
        using var fixture = CreateFixture("[]");
        var request = CreateRequest("999999 Fake Road, Vancouver, BC");

        var response = await fixture.Service.GeocodeAsync(request, CancellationToken.None);

        var result = Assert.Single(response.Results);
        Assert.False(result.Found);
        Assert.Equal("notFound", result.Status);
        Assert.Null(result.Strategy);
        Assert.Single(fixture.Queries);
    }

    [Fact]
    public async Task GeocodeAsync_SkipsFallbackWhenPrimaryLookupSucceeds()
    {
        using var fixture = CreateFixture(PlaceResponse);
        var request = CreateRequest("Apt. 42, 453 West 12th Avenue, Vancouver, BC V5Z 1M2");

        var response = await fixture.Service.GeocodeAsync(request, CancellationToken.None);

        var result = Assert.Single(response.Results);
        Assert.True(result.Found);
        Assert.Equal("normalizedAddress", result.Strategy);
        Assert.Equal(
            "453 West 12th Avenue, Vancouver, BC V5Z 1M2",
            Assert.Single(fixture.Queries));
    }

    [Fact]
    public async Task GeocodeAsync_ReturnsNotFoundWhenBothLookupsFail()
    {
        using var fixture = CreateFixture("[]", "[]");
        var request = CreateRequest("999999 Fake Road, Vancouver, BC V5Z 1M2");

        var response = await fixture.Service.GeocodeAsync(request, CancellationToken.None);

        var result = Assert.Single(response.Results);
        Assert.False(result.Found);
        Assert.Null(result.Strategy);
        Assert.Null(result.Latitude);
        Assert.Null(result.Longitude);
        Assert.Equal(2, fixture.Queries.Count);
    }

    [Fact]
    public async Task GeocodeAsync_UpstreamFailureReturnsFailedAndContinuesBatch()
    {
        using var fixture = CreateFixture("__503__", PlaceResponse);
        var request = new GeocodeRequest(
        [
            new AddressRequest("failed", "100 Failure Road, Vancouver, BC"),
            new AddressRequest("next", "200 Success Road, Vancouver, BC")
        ]);

        var response = await fixture.Service.GeocodeAsync(request, CancellationToken.None);

        Assert.Collection(
            response.Results,
            failed =>
            {
                Assert.Equal("failed", failed.Status);
                Assert.False(failed.Found);
                Assert.Null(failed.Strategy);
                Assert.NotNull(failed.Error);
            },
            successful =>
            {
                Assert.Equal("found", successful.Status);
                Assert.True(successful.Found);
                Assert.Null(successful.Error);
            });
        Assert.Equal(2, fixture.Queries.Count);
    }

    [Fact]
    public async Task GeocodeAsync_CachesSuccessfulNormalizedAddress()
    {
        using var fixture = CreateFixture(PlaceResponse);
        var request = CreateRequest("453 West 12th Avenue, Vancouver, BC V5Z 1M2");

        var first = await fixture.Service.GeocodeAsync(request, CancellationToken.None);
        var second = await fixture.Service.GeocodeAsync(request, CancellationToken.None);

        Assert.True(Assert.Single(first.Results).Found);
        Assert.True(Assert.Single(second.Results).Found);
        Assert.Single(fixture.Queries);
    }

    [Fact]
    public async Task GeocodeAsync_DoesNotCacheNotFoundResult()
    {
        using var fixture = CreateFixture("[]", "[]");
        var request = CreateRequest("999999 Fake Road, Vancouver, BC");

        await fixture.Service.GeocodeAsync(request, CancellationToken.None);
        await fixture.Service.GeocodeAsync(request, CancellationToken.None);

        Assert.Equal(2, fixture.Queries.Count);
    }

    [Fact]
    public async Task GeocodeAsync_ChecksFullAddressBeforeCachedPostalFallback()
    {
        using var fixture = CreateFixture("[]", PlaceResponse, "[]");
        var request = CreateRequest("999999 Fake Road, Vancouver, BC V5Z 1M2");

        var first = await fixture.Service.GeocodeAsync(request, CancellationToken.None);
        var second = await fixture.Service.GeocodeAsync(request, CancellationToken.None);

        Assert.Equal("postalCode", Assert.Single(first.Results).Strategy);
        Assert.Equal("postalCode", Assert.Single(second.Results).Strategy);
        Assert.Equal(
            [
                "999999 Fake Road, Vancouver, BC V5Z 1M2",
                "V5Z 1M2",
                "999999 Fake Road, Vancouver, BC V5Z 1M2"
            ],
            fixture.Queries);
    }

    private static GeocodeRequest CreateRequest(string address)
    {
        return new GeocodeRequest([new AddressRequest("1", address)]);
    }

    private static TestFixture CreateFixture(params string[] responses)
    {
        var handler = new StubHttpMessageHandler(responses);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
        };
        var client = new NominatimClient(
            new StubHttpClientFactory(httpClient),
            Options.Create(new NominatimOptions
            {
                MinimumRequestIntervalMilliseconds = 0
            }),
            NullLogger<NominatimClient>.Instance);

        var sqliteConnection = new SqliteConnection("Data Source=:memory:");
        sqliteConnection.Open();
        var dbContext = new GeocodingCacheDbContext(
            new DbContextOptionsBuilder<GeocodingCacheDbContext>()
                .UseSqlite(sqliteConnection)
                .Options);
        dbContext.Database.EnsureCreated();
        var cacheStore = new GeocodingCacheStore(
            dbContext,
            NullLogger<GeocodingCacheStore>.Instance);
        var service = new GeocodingService(
            client,
            new AddressNormalizer(),
            new CanadianPostalCodeExtractor(),
            cacheStore);

        return new TestFixture(
            service,
            client,
            httpClient,
            handler.Queries,
            dbContext,
            sqliteConnection);
    }

    private sealed class TestFixture(
        GeocodingService service,
        NominatimClient client,
        HttpClient httpClient,
        IReadOnlyList<string> queries,
        GeocodingCacheDbContext dbContext,
        SqliteConnection sqliteConnection) : IDisposable
    {
        public GeocodingService Service { get; } = service;
        public IReadOnlyList<string> Queries { get; } = queries;

        public void Dispose()
        {
            dbContext.Dispose();
            sqliteConnection.Dispose();
            client.Dispose();
            httpClient.Dispose();
        }
    }

    private sealed class StubHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return httpClient;
        }
    }

    private sealed class StubHttpMessageHandler(IEnumerable<string> responses) : HttpMessageHandler
    {
        private readonly Queue<string> _responses = new(responses);

        public List<string> Queries { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Queries.Add(GetQueryValue(request.RequestUri!, "q"));
            var responseBody = _responses.Dequeue();

            if (responseBody == "__503__")
            {
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseBody,
                    Encoding.UTF8,
                    "application/json")
            };

            return Task.FromResult(response);
        }

        private static string GetQueryValue(Uri uri, string name)
        {
            foreach (var parameter in uri.Query.TrimStart('?').Split('&'))
            {
                var parts = parameter.Split('=', 2);

                if (parts[0] == name)
                {
                    return Uri.UnescapeDataString(parts[1]);
                }
            }

            throw new InvalidOperationException($"Query parameter '{name}' was not found.");
        }
    }
}
