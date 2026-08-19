using FluentAssertions;
using MyTradingToolbox.Core.Enums;
using MyTradingToolbox.Core.Utils;
using Xunit;

namespace MyTradingToolbox.Tests;

public class OCCParserTests
{
    [Theory]
    [InlineData("AAPL260918C00220000", "AAPL", 2026, 9, 18, OptionSide.Call, 220.0)]
    [InlineData("SPY261219P00550500", "SPY", 2026, 12, 19, OptionSide.Put, 550.5)]
    [InlineData("MSFT250620C00400000", "MSFT", 2025, 6, 20, OptionSide.Call, 400.0)]
    [InlineData("UMAC260116C00015000", "UMAC", 2026, 1, 16, OptionSide.Call, 15.0)]
    public void TryParse_ValidOCCSymbol_ReturnsCorrectComponents(
        string occ, string expectedUnderlying, int year, int month, int day, OptionSide expectedSide, decimal expectedStrike)
    {
        var success = OCCParser.TryParse(occ, out var underlying, out var exp, out var side, out var strike);

        success.Should().BeTrue();
        underlying.Should().Be(expectedUnderlying);
        exp.Should().Be(new DateOnly(year, month, day));
        side.Should().Be(expectedSide);
        strike.Should().Be(expectedStrike);
    }

    [Fact]
    public void Format_GivenComponents_GeneratesExpectedStandardOCC()
    {
        var formatted = OCCParser.Format("AAPL", new DateOnly(2026, 9, 18), OptionSide.Call, 220.0m);
        formatted.Should().Be("AAPL260918C00220000");

        var parsed = OCCParser.TryParse(formatted, out var underlying, out var exp, out var side, out var strike);
        parsed.Should().BeTrue();
        underlying.Should().Be("AAPL");
        strike.Should().Be(220.0m);
    }

    [Theory]
    [InlineData("")]
    [InlineData("INVALID")]
    [InlineData("AAPL")]
    [InlineData(null)]
    public void TryParse_InvalidInput_ReturnsFalse(string? input)
    {
        var success = OCCParser.TryParse(input ?? "", out _, out _, out _, out _);
        success.Should().BeFalse();
    }
}
