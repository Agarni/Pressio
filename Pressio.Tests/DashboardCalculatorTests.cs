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
    public void Correlations_ComparesAfterFactorVsNoRecentFactor()
    {
        // Café às 8h. Leituras nas 2h seguintes (expostas) 130/135 vs. sem café recente 125/122.
        var baseDay = new DateTime(2026, 9, 1, 6, 0, 0);
        var items = new List<BloodPressureMeasurement>
        {
            M(130, 86, baseDay, MeasurementContext.Caffeine),                      // exposição (não entra nos grupos)
            M(132, 88, baseDay.AddHours(1), MeasurementContext.None),              // exposto após
            M(136, 90, baseDay.AddHours(2), MeasurementContext.None),              // exposto após
            M(138, 92, baseDay.AddHours(3), MeasurementContext.None),              // exposto após
            M(125, 80, baseDay.AddDays(1), MeasurementContext.None),               // sem fator recente
            M(122, 78, baseDay.AddDays(2), MeasurementContext.None),               // sem fator recente
            M(124, 79, baseDay.AddDays(3), MeasurementContext.None),               // sem fator recente
        };
        var correlations = DashboardCalculator.Correlations(items);
        Assert.Single(correlations);
        var c = correlations[0];
        Assert.Equal("Café ou energético", c.Label);
        Assert.True(c.Raises); // expostos mais altos que sem fator
        Assert.Equal(11, c.DeltaSys); // ~135 vs ~124
        Assert.True(c.IsTrend); // 3 (expostos) < 5 e 3 (sem) < 5 -> tendência
    }

    [Fact]
    public void Correlations_ReadingAfterWindowIsUnexposed()
    {
        // Café às 8h, mas a leitura só às 16h (> 6h): não é exposta.
        var baseDay = new DateTime(2026, 9, 1, 8, 0, 0);
        var items = new List<BloodPressureMeasurement>
        {
            M(128, 84, baseDay, MeasurementContext.Caffeine),
            M(150, 100, baseDay.AddHours(8), MeasurementContext.None),
            M(120, 80, baseDay.AddDays(1), MeasurementContext.None),
            M(122, 82, baseDay.AddDays(2), MeasurementContext.None),
            M(121, 81, baseDay.AddDays(3), MeasurementContext.None),
        };
        var correlations = DashboardCalculator.Correlations(items);
        // a única leitura exposta é a de 8h depois; expostos = 1 (<3) -> nenhuma correlação.
        Assert.Empty(correlations);
    }

    [Fact]
    public void Correlations_RequiresMinPerGroup()
    {
        var baseDay = new DateTime(2026, 9, 1, 6, 0, 0);
        var items = new List<BloodPressureMeasurement>
        {
            M(130, 85, baseDay, MeasurementContext.Caffeine),
            M(135, 88, baseDay.AddHours(1), MeasurementContext.None),
            M(125, 80, baseDay.AddDays(1), MeasurementContext.None),
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
