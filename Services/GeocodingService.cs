using GeocoderSolution.Data;
using GeocoderSolution.DTOs;

namespace GeocoderSolution.Services;

public sealed class GeocodingService(
    NominatimClient nominatimClient,
    AddressNormalizer addressNormalizer,
    CanadianPostalCodeExtractor postalCodeExtractor,
    GeocodingCacheStore cacheStore)
{
    private const string NormalizedAddressStrategy = "normalizedAddress";
    private const string PostalCodeStrategy = "postalCode";

    public async Task<GeocodeResponse> GeocodeAsync(
        GeocodeRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<GeocodeResult>(request.Addresses.Count);

        foreach (var input in request.Addresses)
        {
            var normalizedAddress = addressNormalizer.Normalize(input.Address);
            var addressCacheKey = GeocodingCacheKeys.ForAddress(normalizedAddress);
            var cachedEntry = await cacheStore.GetAsync(addressCacheKey, cancellationToken);
            NominatimPlace? place;
            string? strategy;

            if (cachedEntry is not null)
            {
                place = ToPlace(cachedEntry);
                strategy = cachedEntry.Strategy;
            }
            else
            {
                place = await nominatimClient.SearchAsync(
                    normalizedAddress,
                    cancellationToken);
                strategy = place is null ? null : NormalizedAddressStrategy;

                if (place is not null)
                {
                    await cacheStore.SaveAsync(
                        addressCacheKey,
                        place,
                        NormalizedAddressStrategy,
                        cancellationToken);
                }
            }

            if (place is null)
            {
                var postalCode = postalCodeExtractor.Extract(input.Address);

                if (postalCode is not null)
                {
                    var postalCacheKey = GeocodingCacheKeys.ForPostalCode(postalCode);
                    cachedEntry = await cacheStore.GetAsync(
                        postalCacheKey,
                        cancellationToken);

                    if (cachedEntry is not null)
                    {
                        place = ToPlace(cachedEntry);
                        strategy = cachedEntry.Strategy;
                    }
                    else
                    {
                        place = await nominatimClient.SearchAsync(
                            postalCode,
                            cancellationToken);
                        strategy = place is null ? null : PostalCodeStrategy;

                        if (place is not null)
                        {
                            await cacheStore.SaveAsync(
                                postalCacheKey,
                                place,
                                PostalCodeStrategy,
                                cancellationToken);
                        }
                    }
                }
            }

            results.Add(new GeocodeResult(
                input.Id,
                input.Address,
                place is not null,
                strategy,
                place?.Latitude,
                place?.Longitude,
                place?.Name,
                place?.DisplayName));
        }

        return new GeocodeResponse(results);
    }

    private static NominatimPlace ToPlace(GeocodingCacheEntry entry)
    {
        return new NominatimPlace(
            entry.Latitude,
            entry.Longitude,
            entry.Name,
            entry.DisplayName);
    }
}
