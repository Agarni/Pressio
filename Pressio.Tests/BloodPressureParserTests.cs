using Pressio.Services;
using Xunit;

namespace Pressio.Tests;

public class BloodPressureParserTests
{
    [Theory]
    [InlineData("13/8", 130, 80)]
    [InlineData("130/80", 130, 80)]
    [InlineData("13 8", 130, 80)]
    [InlineData("13x8", 130, 80)]
    [InlineData("13X8", 130, 80)]
    [InlineData(" 13 / 8 ", 130, 80)]
    [InlineData("118/78", 118, 78)]
    [InlineData("154/102", 154, 102)]
    [InlineData("80/50", 80, 50)]
    public void TryParse_ValidInput_ReturnsValues(string input, int systolic, int diastolic)
    {
        var ok = BloodPressureParser.TryParse(input, out var result, out var error);
        Assert.True(ok);
        Assert.NotNull(result);
        Assert.Equal(systolic, result!.Systolic);
        Assert.Equal(diastolic, result.Diastolic);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("130")]
    [InlineData("130/80/10")]
    [InlineData("a/b")]
    [InlineData("40/70")]
    [InlineData("400/100")]
    [InlineData("130/250")]
    public void TryParse_InvalidInput_Fails(string input)
    {
        var ok = BloodPressureParser.TryParse(input, out var result, out var error);
        Assert.False(ok);
        Assert.Null(result);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void TryParse_Null_Fails()
    {
        Assert.False(BloodPressureParser.TryParse(null, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void DisplayValue_UsesShorthand()
    {
        Assert.True(BloodPressureParser.TryParse("13/8", out var result, out _));
        Assert.Equal("13/8", result!.DisplayValue);
    }
}
