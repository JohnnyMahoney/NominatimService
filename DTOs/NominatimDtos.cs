using System.Text.Json.Serialization;

namespace GeocoderSolution.DTOs;

public sealed record NominatimSearchResult([property: JsonPropertyName("lat")] string Latitude, [property: JsonPropertyName("lon")] string Longitude, [property: JsonPropertyName("name")] string? Name, [property: JsonPropertyName("display_name")] string? DisplayName);

public sealed record NominatimPlace(double Latitude, double Longitude, string? Name, string? DisplayName);
