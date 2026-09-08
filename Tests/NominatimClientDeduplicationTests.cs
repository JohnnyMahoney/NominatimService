using System.Collections.Concurrent;
using System.Net;
using System.Text;
using GeocoderSolution.Configuration;
using GeocoderSolution.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GeocoderSolution.Tests;

public sealed class NominatimClientDeduplicationTests
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
    public async Task SearchAsync_ConcurrentEquivalentQueriesShareOneOutboundRequest()
    {
        using var handler = new BlockingHttpMessageHandler(PlaceResponse);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
        };
        var logger = new RecordingLogger<NominatimClient>();
        using var client = CreateClient(httpClient, logger);

        var first = client.SearchAsync("V5Z  1M2", CancellationToken.None);
        await handler.RequestStarted;

        var duplicate = client.SearchAsync("  v5z 1m2  ", CancellationToken.None);

        Assert.Equal(1, handler.RequestCount);
        handler.CompleteRequest();

        var results = await Task.WhenAll(first, duplicate);

        Assert.All(results, Assert.NotNull);
        Assert.Equal(1, handler.RequestCount);
        Assert.Contains(
            logger.Messages,
            message => message.Contains("Sending outbound request to Nominatim"));
        Assert.Contains(
            logger.Messages,
            message => message.Contains("Reusing in-flight Nominatim request"));
    }

    [Fact]
    public async Task SearchAsync_AfterCompletionStartsANewOutboundRequest()
    {
        using var handler = new BlockingHttpMessageHandler(PlaceResponse, initiallyReleased: true);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
        };
        using var client = CreateClient(httpClient, new RecordingLogger<NominatimClient>());

        await client.SearchAsync("V5Z 1M2", CancellationToken.None);
        await client.SearchAsync("v5z 1m2", CancellationToken.None);

        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task SearchAsync_AfterFailureRemovesInFlightEntry()
    {
        using var handler = new FailOnceHttpMessageHandler(PlaceResponse);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org/")
        };
        using var client = CreateClient(httpClient, new RecordingLogger<NominatimClient>());

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SearchAsync("V5Z 1M2", CancellationToken.None));
        var result = await client.SearchAsync("V5Z 1M2", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, handler.RequestCount);
    }

    private static NominatimClient CreateClient(
        HttpClient httpClient,
        ILogger<NominatimClient> logger)
    {
        return new NominatimClient(
            new StubHttpClientFactory(httpClient),
            Options.Create(new NominatimOptions
            {
                MinimumRequestIntervalMilliseconds = 0
            }),
            logger);
    }

    private sealed class StubHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => httpClient;
    }

    private sealed class BlockingHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _response;
        private readonly TaskCompletionSource _requestStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseRequest =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _requestCount;

        public BlockingHttpMessageHandler(string response, bool initiallyReleased = false)
        {
            _response = response;

            if (initiallyReleased)
            {
                _releaseRequest.SetResult();
            }
        }

        public Task RequestStarted => _requestStarted.Task;
        public int RequestCount => Volatile.Read(ref _requestCount);

        public void CompleteRequest() => _releaseRequest.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            _requestStarted.TrySetResult();
            await _releaseRequest.Task.WaitAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_response, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class FailOnceHttpMessageHandler(string successfulResponse)
        : HttpMessageHandler
    {
        private int _requestCount;

        public int RequestCount => Volatile.Read(ref _requestCount);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var requestNumber = Interlocked.Increment(ref _requestCount);
            var response = requestNumber == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        successfulResponse,
                        Encoding.UTF8,
                        "application/json")
                };

            return Task.FromResult(response);
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IReadOnlyCollection<string> Messages => _messages;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _messages.Enqueue(formatter(state, exception));
        }
    }
}
