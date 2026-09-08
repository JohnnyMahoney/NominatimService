using System.Text.RegularExpressions;

namespace GeocoderSolution.Services;

public sealed class CanadianPostalCodeExtractor
{
    // Input: "Vancouver, BC V5Y1V4" -> Output: "V5Y 1V4"
    private static readonly Regex PostalCode = new(
        @"(?<![A-Z0-9])([ABCEGHJ-NPRSTVXY]\d[ABCEGHJ-NPRSTV-Z]) ?(\d[ABCEGHJ-NPRSTV-Z]\d)(?![A-Z0-9])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public string? Extract(string address)
    {
        ArgumentNullException.ThrowIfNull(address);

        var match = PostalCode.Match(address);

        if (!match.Success)
        {
            return null;
        }

        return $"{match.Groups[1].Value.ToUpperInvariant()} " +
               match.Groups[2].Value.ToUpperInvariant();
    }
}
