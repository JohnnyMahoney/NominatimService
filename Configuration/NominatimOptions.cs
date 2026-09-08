namespace GeocoderSolution.Configuration;

public sealed class NominatimOptions
{
    public const string SectionName = "Nominatim";

    public string BaseUrl { get; init; } = string.Empty;
    public string UserAgent { get; init; } = string.Empty;
    public int MinimumRequestIntervalMilliseconds { get; init; }
    public int TimeoutSeconds { get; init; }
}
