using System;
using System.Collections.Generic;
using System.Linq;
using Pressio.Models;
using Pressio.Services;
using Xunit;

namespace Pressio.Tests;

public class MeasurementFilterTests
{
    private static readonly DateTime Today = new(2026, 9, 7, 12, 0, 0);

    // data: (mês, dia, hora, horário da medicação, notas)
    private readonly List<BloodPressureMeasurement> _all = new()
    {
        M(9, 7, 7, Before, "Dor de cabeça"),   // hoje, manhã, antes
        M(9, 7, 22, After, "Café"),            // hoje, noite, depois
        M(9, 7, 3, Before, "Café"),            // hoje, madrugada, antes
        M(9, 6, 8, Before, null),              // ontem (dentro dos 7 dias), manhã
        M(8, 30, 9, Before, "Café"),           // fora dos 7 dias, manhã
    };

    private static BloodPressureMeasurement M(int month, int day, int hour, MedicationTiming timing, string? notes)
        => new(140, 90, new DateTime(2026, month, day, hour, 0, 0), timing, notes);

    private const MedicationTiming Before = MedicationTiming.BeforeMedication;
    private const MedicationTiming After = MedicationTiming.AfterMedication;

    [Fact]
    public void All_ByDefault() =>
        Assert.Equal(_all.Count, MeasurementFilter.Apply(_all, "Todo o histórico", "Todas", "Todos os horários", "", Today).Count);

    [Fact]
    public void Period_Today()
    {
        var result = MeasurementFilter.Apply(_all, "Hoje", "Todas", "Todos os horários", "", Today);
        Assert.Equal(3, result.Count);
        Assert.All(result, m => Assert.True(m.MeasuredAt.Date == Today.Date));
    }

    [Fact]
    public void Period_Last7Days_ExcludesOlder()
    {
        var result = MeasurementFilter.Apply(_all, "Últimos 7 dias", "Todas", "Todos os horários", "", Today);
        Assert.DoesNotContain(result, m => m.MeasuredAt < Today.AddDays(-6));
    }

    [Fact]
    public void Medication_FiltersTiming()
    {
        var result = MeasurementFilter.Apply(_all, "Todo o histórico", "Antes da medicação", "Todos os horários", "", Today);
        Assert.Equal(4, result.Count);
        Assert.All(result, m => Assert.True(m.MedicationTiming == Before));
    }

    [Fact]
    public void TimeOfDay_Manha()
    {
        var result = MeasurementFilter.Apply(_all, "Todo o histórico", "Todas", "Manhã", "", Today);
        Assert.Equal(3, result.Count);
        Assert.All(result, m => Assert.InRange(m.MeasuredAt.Hour, 6, 11));
    }

    [Fact]
    public void TimeOfDay_Madrugada()
    {
        var result = MeasurementFilter.Apply(_all, "Todo o histórico", "Todas", "Madrugada", "", Today);
        var item = Assert.Single(result);
        Assert.True(item.MeasuredAt.Hour < 6);
    }

    [Fact]
    public void Search_Notes_CaseInsensitive()
    {
        var result = MeasurementFilter.Apply(_all, "Todo o histórico", "Todas", "Todos os horários", "café", Today);
        Assert.Equal(3, result.Count);
        Assert.All(result, m => Assert.True(m.Notes!.Contains("café", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Composed_PeriodAndMedication()
    {
        var result = MeasurementFilter.Apply(_all, "Hoje", "Antes da medicação", "Todos os horários", "", Today);
        Assert.Equal(2, result.Count);
        Assert.All(result, m => Assert.True(m.MeasuredAt.Date == Today.Date && m.MedicationTiming == Before));
    }

    [Fact]
    public void Search_ReturnsEmpty_WhenNoMatch() =>
        Assert.Empty(MeasurementFilter.Apply(_all, "Todo o histórico", "Todas", "Todos os horários", "inexistente", Today));
}
