using Xunit;
using Pressio.Models;

namespace Pressio.Tests;

public class ClassificationTests
{
    [Theory]
    [InlineData(105, 65, PressureCategory.Optimal)]
    [InlineData(118, 75, PressureCategory.Optimal)]
    [InlineData(122, 78, PressureCategory.Normal)]
    [InlineData(129, 84, PressureCategory.Normal)]
    [InlineData(135, 80, PressureCategory.Elevated)]
    [InlineData(125, 88, PressureCategory.Elevated)]
    [InlineData(150, 95, PressureCategory.Stage1)]
    [InlineData(138, 95, PressureCategory.Stage1)]
    [InlineData(165, 105, PressureCategory.Stage2)]
    [InlineData(155, 108, PressureCategory.Stage2)]
    [InlineData(185, 100, PressureCategory.Stage3)]
    [InlineData(150, 115, PressureCategory.Stage3)]
    [InlineData(120, 120, PressureCategory.Stage3)] // diastólica alta
    public void Classify_UsesTheWorstOfSystolicAndDiastolic(int sys, int dia, PressureCategory expected)
        => Assert.Equal(expected, BloodPressureClassification.Classify(sys, dia));

    [Fact]
    public void Categories_HaveLabels()
    {
        Assert.Equal("Ótima", BloodPressureClassification.Label(PressureCategory.Optimal));
        Assert.Equal("Normal", BloodPressureClassification.Label(PressureCategory.Normal));
        Assert.Equal("Elevada", BloodPressureClassification.Label(PressureCategory.Elevated));
        Assert.Equal("Hipertensão 1", BloodPressureClassification.Label(PressureCategory.Stage1));
        Assert.Equal("Hipertensão 2", BloodPressureClassification.Label(PressureCategory.Stage2));
        Assert.Equal("Hipertensão 3", BloodPressureClassification.Label(PressureCategory.Stage3));
    }
}
