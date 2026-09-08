using System.Text.RegularExpressions;

namespace GeocoderSolution.Services;

public sealed class AddressNormalizer
{
    // Input: "Apt. 42, 453 West 12th Avenue" -> Output: "453 West 12th Avenue"
    private static readonly Regex NamedUnitQualifier = new(
        @"^\s*(?:Apt\.?|Unit|Suite)\s+(?=[A-Za-z0-9-]*\d)[A-Za-z0-9-]+\s*,?\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Input: "#42, 453 West 12th Avenue" -> Output: "453 West 12th Avenue"
    private static readonly Regex HashUnitQualifier = new(
        @"^\s*#\s*(?=[A-Za-z0-9-]*\d)[A-Za-z0-9-]+\s*,?\s*",
        RegexOptions.CultureInvariant);

    // Input: "123-12 Main St" -> Output: "123 Main St"
    private static readonly Regex DashUnitQualifier = new(
        @"^(\s*\d+)\s*-\s*\d+(?=\s)",
        RegexOptions.CultureInvariant);

    // Input: "453   West  12th Avenue" -> Output: "453 West 12th Avenue"
    private static readonly Regex MultipleWhitespace = new(
        @"\s+",
        RegexOptions.CultureInvariant);

    // Input: "453 West 12th Avenue ,Vancouver" -> Output: "453 West 12th Avenue, Vancouver"
    private static readonly Regex CommaSpacing = new(
        @"\s*,\s*",
        RegexOptions.CultureInvariant);

    public string Normalize(string address)
    {
        ArgumentNullException.ThrowIfNull(address);

        var normalized = NamedUnitQualifier.Replace(address, string.Empty);
        normalized = HashUnitQualifier.Replace(normalized, string.Empty);
        normalized = DashUnitQualifier.Replace(normalized, "$1");
        normalized = MultipleWhitespace.Replace(normalized, " ");
        normalized = CommaSpacing.Replace(normalized, ", ");

        return normalized.Trim(' ', ',');
    }
}
