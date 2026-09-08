namespace GeocoderSolution.Data;

public static class GeocodingCacheKeys
{
    public static string ForAddress(string normalizedAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedAddress);

        var canonicalAddress = string.Join(
                ' ',
                normalizedAddress.Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();

        return $"address:{canonicalAddress}";
    }

    public static string ForPostalCode(string postalCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(postalCode);

        var canonicalPostalCode = string.Concat(
                postalCode.Where(character => !char.IsWhiteSpace(character)))
            .ToUpperInvariant();

        return $"postal:{canonicalPostalCode}";
    }
}
