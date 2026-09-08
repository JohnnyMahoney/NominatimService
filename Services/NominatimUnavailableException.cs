namespace GeocoderSolution.Services;

public sealed class NominatimUnavailableException(string message, Exception innerException) : Exception(message, innerException);
