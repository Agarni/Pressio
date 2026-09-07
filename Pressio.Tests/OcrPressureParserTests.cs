using Pressio.Services;
using Xunit;

namespace Pressio.Tests;

public class OcrPressureParserTests
{
    [Theory]
    [InlineData("138 89", 138, 89)]
    [InlineData("SYS 138 / DIA 89", 138, 89)]
    [InlineData("138/89", 138, 89)]
    [InlineData("13 8", 130, 80)]
    [InlineData("13/8", 130, 80)]
    [InlineData("PA: 146 93 x", 146, 93)]
    [InlineData("120/80 pulse 72", 120, 80)]
    public void Parse_FindsValidPair(string text, int sys, int dia)
    {
        var result = OcrPressureParser.Parse(text);
        Assert.NotNull(result);
        Assert.Equal(sys, result!.Value.Systolic);
        Assert.Equal(dia, result.Value.Diastolic);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello world")]
    [InlineData("12")]
    [InlineData("220 240")] // sistólica < diastólica -> inválido
    public void Parse_ReturnsNull_WhenNoValidPair(string text)
    {
        Assert.Null(OcrPressureParser.Parse(text));
    }

    [Fact]
    public void Parse_IgnoresNonDigitNoise()
    {
        Assert.Equal((130, 80), OcrPressureParser.Parse("mmHg: 13 8 ok"));
    }
}
