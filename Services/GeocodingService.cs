using GeocoderSolution.Data;
using GeocoderSolution.DTOs;

namespace GeocoderSolution.Services;

public sealed class GeocodingService(NominatimClient nominatimClient, AddressNormalizer addressNormalizer, CanadianPostalCodeExtractor postalCodeExtractor, GeocodingCacheStore cacheStore)
{
    private const string NormalizedAddressStrategy = "normalizedAddress";
    private const string PostalCodeStrategy = "postalCode";
    private const string FoundStatus = "found";
    private const string NotFoundStatus = "notFound";
    private const string FailedStatus = "failed";
    private const string UpstreamFailureMessage =
        "The geocoding provider is temporarily unavailable.";

    public async Task<GeocodeResponse> GeocodeAsync(GeocodeRequest request, CancellationToken cancellationToken)
    {
        var results = new List<GeocodeResult>(request.Addresses.Count);

        foreach (var input in request.Addresses)
        {
            try
            {
                results.Add(await GeocodeOneAsync(input, cancellationToken));
            }
            catch (NominatimUnavailableException)
            {
                results.Add(CreateFailedResult(input));
            }
        }

        return new GeocodeResponse(results);
    }

    private async Task<GeocodeResult> GeocodeOneAsync(AddressRequest input, CancellationToken cancellationToken)
    {
        var normalizedAddress = addressNormalizer.Normalize(input.Address);
        var addressCacheKey = GeocodingCacheKeys.ForAddress(normalizedAddress);
        var cachedEntry = await cacheStore.GetAsync(addressCacheKey, cancellationToken);

        if (cachedEntry is not null)
        {
            return CreateFoundResult(
                input,
                ToPlace(cachedEntry),
                cachedEntry.Strategy);
        }

        var place = await nominatimClient.SearchAsync(
            normalizedAddress,
            cancellationToken);

        if (place is not null)
        {
            await cacheStore.SaveAsync(
                addressCacheKey,
                place,
                NormalizedAddressStrategy,
                cancellationToken);

            return CreateFoundResult(
                input,
                place,
                NormalizedAddressStrategy);
        }

        var postalCode = postalCodeExtractor.Extract(input.Address);

        if (postalCode is null)
        {
            return CreateNotFoundResult(input);
        }

        var postalCacheKey = GeocodingCacheKeys.ForPostalCode(postalCode);
        cachedEntry = await cacheStore.GetAsync(postalCacheKey, cancellationToken);

        if (cachedEntry is not null)
        {
            return CreateFoundResult(
                input,
                ToPlace(cachedEntry),
                cachedEntry.Strategy);
        }

        place = await nominatimClient.SearchAsync(postalCode, cancellationToken);

        if (place is null)
        {
            return CreateNotFoundResult(input);
        }

        await cacheStore.SaveAsync(
            postalCacheKey,
            place,
            PostalCodeStrategy,
            cancellationToken);

        return CreateFoundResult(input, place, PostalCodeStrategy);
    }

    private static NominatimPlace ToPlace(GeocodingCacheEntry entry)
    {
        return new NominatimPlace(
            entry.Latitude,
            entry.Longitude,
            entry.Name,
            entry.DisplayName);
    }

    private static GeocodeResult CreateFoundResult(AddressRequest input, NominatimPlace place, string strategy)
    {
        return new GeocodeResult(
            input.Id,
            input.Address,
            FoundStatus,
            true,
            strategy,
            place.Latitude,
            place.Longitude,
            place.Name,
            place.DisplayName,
            null);
    }

    private static GeocodeResult CreateNotFoundResult(AddressRequest input) =>
        new(
            input.Id,
            input.Address,
            NotFoundStatus,
            false,
            null,
            null,
            null,
            null,
            null,
            null);

    private static GeocodeResult CreateFailedResult(AddressRequest input) =>
        new(
            input.Id,
            input.Address,
            FailedStatus,
            false,
            null,
            null,
            null,
            null,
            null,
            UpstreamFailureMessage);
}
