using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using GeocoderSolution.Configuration;
using GeocoderSolution.DTOs;
using Microsoft.Extensions.Options;

namespace GeocoderSolution.Services;

public sealed class NominatimClient : IDisposable
{
    public const string HttpClientName = "Nominatim";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NominatimClient> _logger;
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private readonly ConcurrentDictionary<string, Lazy<Task<NominatimPlace?>>> _inFlight = new();
    private readonly TimeSpan _minimumRequestInterval;
    private long? _lastRequestTimestamp;

    public NominatimClient(
        IHttpClientFactory httpClientFactory,
        IOptions<NominatimOptions> options,
        ILogger<NominatimClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _minimumRequestInterval = TimeSpan.FromMilliseconds(
            options.Value.MinimumRequestIntervalMilliseconds);
    }

    public async Task<NominatimPlace?> SearchAsync(string address, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        cancellationToken.ThrowIfCancellationRequested();

        var key = NormalizeDeduplicationKey(address);
        Lazy<Task<NominatimPlace?>>? newEntry = null;
        newEntry = new Lazy<Task<NominatimPlace?>>(
            () => SearchAndRemoveAsync(key, address, newEntry!),
            LazyThreadSafetyMode.ExecutionAndPublication);

        var inFlightEntry = _inFlight.GetOrAdd(key, newEntry);

        if (!ReferenceEquals(inFlightEntry, newEntry))
        {
            _logger.LogInformation(
                "Reusing in-flight Nominatim request for query {Query}; key {DeduplicationKey}",
                address,
                key);
        }

        return await inFlightEntry.Value.WaitAsync(cancellationToken);
    }

    private async Task<NominatimPlace?> SearchAndRemoveAsync(
        string key,
        string address,
        Lazy<Task<NominatimPlace?>> entry)
    {
        try
        {
            // The shared operation must not be cancelled by one of its callers.
            return await SendSearchAsync(address, CancellationToken.None);
        }
        finally
        {
            ((ICollection<KeyValuePair<string, Lazy<Task<NominatimPlace?>>>>)_inFlight)
                .Remove(new KeyValuePair<string, Lazy<Task<NominatimPlace?>>>(key, entry));
        }
    }

    private async Task<NominatimPlace?> SendSearchAsync(
        string address,
        CancellationToken cancellationToken)
    {
        await _requestGate.WaitAsync(cancellationToken);

        try
        {
            if (_lastRequestTimestamp is { } lastRequestTimestamp)
            {
                var elapsed = Stopwatch.GetElapsedTime(lastRequestTimestamp);
                var remainingDelay = _minimumRequestInterval - elapsed;

                if (remainingDelay > TimeSpan.Zero)
                {
                    await Task.Delay(remainingDelay, cancellationToken);
                }
            }

            _lastRequestTimestamp = Stopwatch.GetTimestamp();

            var requestUri = $"search?q={Uri.EscapeDataString(address)}" +
                             "&format=jsonv2&limit=1&countrycodes=ca";
            var httpClient = _httpClientFactory.CreateClient(HttpClientName);

            _logger.LogInformation(
                "Sending outbound request to Nominatim for query {Query}",
                address);

            NominatimSearchResult[]? response;

            try
            {
                response = await httpClient.GetFromJsonAsync<NominatimSearchResult[]>(
                    requestUri,
                    cancellationToken);
            }
            catch (OperationCanceledException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Nominatim request timed out for query {Query}",
                    address);
                throw new NominatimUnavailableException(
                    "The Nominatim request timed out.",
                    exception);
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Nominatim request failed for query {Query}; status code {StatusCode}",
                    address,
                    exception.StatusCode);
                throw new NominatimUnavailableException(
                    "Nominatim is temporarily unavailable.",
                    exception);
            }
            catch (JsonException exception)
            {
                _logger.LogError(
                    exception,
                    "Nominatim returned invalid JSON for query {Query}",
                    address);
                throw new NominatimUnavailableException(
                    "Nominatim returned an invalid response.",
                    exception);
            }

            var firstResult = response?.FirstOrDefault();

            if (firstResult is null)
            {
                return null;
            }

            return new NominatimPlace(
                double.Parse(firstResult.Latitude, CultureInfo.InvariantCulture),
                double.Parse(firstResult.Longitude, CultureInfo.InvariantCulture),
                firstResult.Name,
                firstResult.DisplayName);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private static string NormalizeDeduplicationKey(string query)
    {
        return string.Join(
                ' ',
                query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
    }

    public void Dispose()
    {
        _requestGate.Dispose();
    }
}
