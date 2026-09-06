using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Pressio.Models;
using Pressio.Services;

namespace Pressio.Tests;

public class DashboardCalculatorTests
{
    private static BloodPressureMeasurement M(int sys, int dia, DateTime when, MeasurementContext ctx = MeasurementContext.None, MedicationTiming med = MedicationTiming.NotInformed)
        => new BloodPressureMeasurement(sys, dia, when, med, null, ctx);

    [Fact]
    public void TimeDistribution_ComputesAveragesAndCounts()
    {
        BloodPressureMeasurement.UseShorthandFormat = false;
        var items = new List<BloodPressureMeasurement>
        {
            M(120, 80, new DateTime(2026, 9, 1, 8, 0, 0)),   // manhã
            M(130, 85, new DateTime(2026, 9, 1, 9, 0, 0)),   // manhã
            M(140, 90, new DateTime(2026, 9, 1, 15, 0, 0)),  // tarde
            M(150, 95, new DateTime(2026, 9, 1, 21, 0, 0)),  // noite
        };
        var slots = DashboardCalculator.TimeDistribution(items).ToDictionary(s => s.Label);

        Assert.Equal(2, slots["Manhã"].Count);
        Assert.Equal("125/83", slots["Manhã"].AverageText);
        Assert.Equal(1, slots["Tarde"].Count);
        Assert.Equal("0", slots["Madrugada"].Count.ToString());
        Assert.Equal("—", slots["Madrugada"].AverageText);
    }

    [Fact]
    public void ContextCounts_CountsOnlyPresentFactors()
    {
        var items = new List<BloodPressureMeasurement>
        {
            M(120, 80, DateTime.Now, MeasurementContext.Caffeine),
            M(130, 85, DateTime.Now, MeasurementContext.Caffeine | MeasurementContext.Stress),
            M(140, 90, DateTime.Now, MeasurementContext.None),
        };
        var counts = DashboardCalculator.ContextCounts(items).ToDictionary(c => c.Label, c => c.Count);
        Assert.Equal(2, counts["Café ou energético"]);
        Assert.Equal(1, counts["Estresse ou ansiedade"]);
    }

    [Fact]
    public void Correlations_ComparesWithAndWithoutFactor()
    {
        var items = new List<BloodPressureMeasurement>
        {
            M(130, 85, DateTime.Now, MeasurementContext.Caffeine),
            M(135, 88, DateTime.Now, MeasurementContext.Caffeine),
            M(125, 80, DateTime.Now, MeasurementContext.None),
            M(122, 78, DateTime.Now, MeasurementContext.None),
        };
        var correlations = DashboardCalculator.Correlations(items);
        Assert.Single(correlations);
        var c = correlations[0];
        Assert.Equal("Café ou energético", c.Label);
        Assert.True(c.Raises); // com café mais alto que sem
        Assert.Equal(9, c.DeltaSys); // 132 vs 124 (arredondados)
    }

    [Fact]
    public void Correlations_RequiresTwoEachSide()
    {
        var items = new List<BloodPressureMeasurement>
        {
            M(130, 85, DateTime.Now, MeasurementContext.Caffeine),
            M(125, 80, DateTime.Now, MeasurementContext.None),
        };
        Assert.Empty(DashboardCalculator.Correlations(items));
    }

    [Fact]
    public void MedicationSummary_ReturnsDashWhenNone()
        => Assert.Equal("—", DashboardCalculator.MedicationSummary(new List<BloodPressureMeasurement>(), MedicationTiming.BeforeMedication));

    [Fact]
    public void FilterByPeriod_SevenDays_FiltersByDate()
    {
        var today = DateTime.Today;
        var items = new List<BloodPressureMeasurement>
        {
            M(120, 80, today.AddHours(8)),          // hoje
            M(130, 85, today.AddDays(-2).AddHours(8)), // 2 dias atrás
            M(140, 90, today.AddDays(-10).AddHours(8)), // fora
        };
        var (result, truncated) = DashboardCalculator.FilterByPeriod(items, "Últimos 7 dias", null, null);
        Assert.Equal(2, result.Count);
        Assert.False(truncated);
    }

    [Fact]
    public void FilterByPeriod_CustomRange()
    {
        var items = new List<BloodPressureMeasurement>
        {
            M(120, 80, new DateTime(2026, 8, 1, 8, 0, 0)),
            M(130, 85, new DateTime(2026, 8, 10, 8, 0, 0)),
            M(140, 90, new DateTime(2026, 8, 20, 8, 0, 0)),
        };
        var (result, _) = DashboardCalculator.FilterByPeriod(items, "Período personalizado", new DateTime(2026, 8, 5), new DateTime(2026, 8, 12));
        Assert.Single(result);
        Assert.Equal(new DateTime(2026, 8, 10, 8, 0, 0), result[0].MeasuredAt);
    }

    [Fact]
    public void FilterByPeriod_TruncatesAt30()
    {
        var items = Enumerable.Range(1, 40)
            .Select(i => M(100 + i, 60, DateTime.Today.AddDays(-i).AddHours(8)))
            .ToList();
        var (result, truncated) = DashboardCalculator.FilterByPeriod(items, "Todo o histórico", null, null);
        Assert.Equal(30, result.Count);
        Assert.True(truncated);
    }
}
