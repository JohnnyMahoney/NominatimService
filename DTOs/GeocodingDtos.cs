namespace GeocoderSolution.DTOs;

public sealed record GeocodeRequest(IReadOnlyList<AddressRequest> Addresses);

public sealed record AddressRequest(string Id, string Address);

public sealed record GeocodeResponse(IReadOnlyList<GeocodeResult> Results);

public sealed record GeocodeResult(string Id, string Address, string Status, bool Found, string? Strategy, double? Latitude, double? Longitude, string? Name, string? DisplayName, string? Error);
