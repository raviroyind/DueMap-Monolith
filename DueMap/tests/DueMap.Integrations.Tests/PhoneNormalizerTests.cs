using DueMap.Integrations.Notices;
using Xunit;

namespace DueMap.Integrations.Tests;

/// <summary>
/// P2-2: phone matching collapses free-form accounting-sync numbers and
/// Twilio's E.164 to the same comparable key (US last-10).
/// </summary>
public sealed class PhoneNormalizerTests
{
    [Theory]
    [InlineData("+1 (415) 555-1234", "4155551234")]
    [InlineData("14155551234",       "4155551234")]
    [InlineData("415-555-1234",      "4155551234")]
    [InlineData("4155551234",        "4155551234")]
    [InlineData("+14155551234",      "4155551234")]
    public void Various_formats_collapse_to_last_ten(string input, string expected)
    {
        Assert.Equal(expected, PhoneNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no digits here")]
    public void Empty_or_no_digits_returns_empty(string? input)
    {
        Assert.Equal(string.Empty, PhoneNormalizer.Normalize(input));
    }

    [Fact]
    public void Short_number_kept_as_is()
    {
        Assert.Equal("5551234", PhoneNormalizer.Normalize("555-1234"));
    }
}
