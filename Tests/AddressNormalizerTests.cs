using GeocoderSolution.Services;

namespace GeocoderSolution.Tests;

public sealed class AddressNormalizerTests
{
    private readonly AddressNormalizer _normalizer = new();

    [Theory]
    [InlineData("Apt. 42, 453 West 12th Avenue, Vancouver, BC")]
    [InlineData("Apt 42, 453 West 12th Avenue, Vancouver, BC")]
    [InlineData("Unit 42, 453 West 12th Avenue, Vancouver, BC")]
    [InlineData("Suite 42, 453 West 12th Avenue, Vancouver, BC")]
    [InlineData("#42, 453 West 12th Avenue, Vancouver, BC")]
    public void Normalize_RemovesLeadingUnitQualifier(string address)
    {
        var result = _normalizer.Normalize(address);

        Assert.Equal("453 West 12th Avenue, Vancouver, BC", result);
    }

    [Fact]
    public void Normalize_RemovesDashUnitNumber()
    {
        var result = _normalizer.Normalize(
            "123-12 Main St, Vancouver, BC");

        Assert.Equal("123 Main St, Vancouver, BC", result);
    }

    [Fact]
    public void Normalize_CollapsesExtraWhitespace()
    {
        var result = _normalizer.Normalize(
            "453   West  12th Avenue, Vancouver,  BC");

        Assert.Equal("453 West 12th Avenue, Vancouver, BC", result);
    }

    [Fact]
    public void Normalize_CleansCommaSpacingAndOuterPunctuation()
    {
        var result = _normalizer.Normalize(
            " , 453 West 12th Avenue ,Vancouver ,  BC, ");

        Assert.Equal("453 West 12th Avenue, Vancouver, BC", result);
    }

    [Fact]
    public void Normalize_LeavesNormalizedAddressUnchanged()
    {
        const string address = "453 West 12th Avenue, Vancouver, BC V5Z 1M2";

        var result = _normalizer.Normalize(address);

        Assert.Equal(address, result);
    }
}
