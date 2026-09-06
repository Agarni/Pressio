using System;
using System.Collections.Generic;
using System.Linq;
using Pressio.Models;

namespace Pressio.Services;

// Cálculos do dashboard isolados (sem UI/App/Supabase) para serem testáveis.
public static class DashboardCalculator
{
    public static IReadOnlyList<TimeSlotInfo> TimeDistribution(IReadOnlyList<BloodPressureMeasurement> items) => new[]
    {
        BuildSlot("Madrugada", items.Where(x => x.MeasuredAt.Hour < 6)),
        BuildSlot("Manhã", items.Where(x => x.MeasuredAt.Hour >= 6 && x.MeasuredAt.Hour < 12)),
        BuildSlot("Tarde", items.Where(x => x.MeasuredAt.Hour >= 12 && x.MeasuredAt.Hour < 18)),
        BuildSlot("Noite", items.Where(x => x.MeasuredAt.Hour >= 18)),
    };

    private static TimeSlotInfo BuildSlot(string label, IEnumerable<BloodPressureMeasurement> subset)
    {
        var list = subset.ToList();
        if (list.Count == 0) return new TimeSlotInfo(label, 0, "—");
        var systolic = (int)Math.Round(list.Average(x => x.Systolic), MidpointRounding.AwayFromZero);
        var diastolic = (int)Math.Round(list.Average(x => x.Diastolic), MidpointRounding.AwayFromZero);
        return new TimeSlotInfo(label, list.Count, BloodPressureMeasurement.Format(systolic, diastolic));
    }

    public static IReadOnlyList<ContextCountInfo> ContextCounts(IReadOnlyList<BloodPressureMeasurement> items)
    {
        var result = new List<ContextCountInfo>();
        foreach (var (value, label) in MeasurementContextInfo.AllContexts)
        {
            var count = items.Count(x => (x.Context & value) != 0);
            if (count > 0) result.Add(new ContextCountInfo(label, count));
        }
        return result;
    }

    public static IReadOnlyList<CorrelationInfo> Correlations(IReadOnlyList<BloodPressureMeasurement> items)
    {
        var result = new List<CorrelationInfo>();
        foreach (var (value, label) in MeasurementContextInfo.AllContexts)
        {
            var with = items.Where(x => (x.Context & value) != 0).ToList();
            if (with.Count < 2) continue;
            var without = items.Where(x => (x.Context & value) == 0).ToList();
            if (without.Count < 2) continue;
            var ws = (int)Math.Round(with.Average(x => x.Systolic), MidpointRounding.AwayFromZero);
            var wd = (int)Math.Round(with.Average(x => x.Diastolic), MidpointRounding.AwayFromZero);
            var ns = (int)Math.Round(without.Average(x => x.Systolic), MidpointRounding.AwayFromZero);
            var nd = (int)Math.Round(without.Average(x => x.Diastolic), MidpointRounding.AwayFromZero);
            var ds = ws - ns;
            var dd = wd - nd;
            if (ds == 0 && dd == 0) continue;
            var delta = $"{(ds >= 0 ? "+" : "")}{ds}/{(dd >= 0 ? "+" : "")}{dd}";
            var detail = $"com: {with.Count}x {BloodPressureMeasurement.Format(ws, wd)}  ·  sem: {without.Count}x {BloodPressureMeasurement.Format(ns, nd)}";
            result.Add(new CorrelationInfo(label, delta, detail, ds > 0 || dd > 0, ds, dd));
        }
        return result
            .OrderByDescending(c => Math.Max(Math.Abs(c.DeltaSys), Math.Abs(c.DeltaDia)))
            .ToList();
    }

    public static string MedicationSummary(IReadOnlyList<BloodPressureMeasurement> items, MedicationTiming timing)
    {
        var subset = items.Where(x => x.MedicationTiming == timing).ToList();
        if (subset.Count == 0) return "—";
        var systolic = (int)Math.Round(subset.Average(x => x.Systolic), MidpointRounding.AwayFromZero);
        var diastolic = (int)Math.Round(subset.Average(x => x.Diastolic), MidpointRounding.AwayFromZero);
        return $"{subset.Count}x  ·  média {BloodPressureMeasurement.Format(systolic, diastolic)}";
    }

    // Filtra por período (para relatórios): Todo / 7 / 30 / personalizado, limitado a 30 registros.
    public static (List<BloodPressureMeasurement> Items, bool Truncated) FilterByPeriod(
        IEnumerable<BloodPressureMeasurement> items, string period, DateTime? start, DateTime? end)
    {
        IEnumerable<BloodPressureMeasurement> query = items;
        switch (period)
        {
            case "Últimos 7 dias":
                var from7 = DateTime.Today.AddDays(-6);
                query = query.Where(m => m.MeasuredAt.Date >= from7);
                break;
            case "Últimos 30 dias":
                var from30 = DateTime.Today.AddDays(-29);
                query = query.Where(m => m.MeasuredAt.Date >= from30);
                break;
            case "Período personalizado":
                if (start is { } s) query = query.Where(m => m.MeasuredAt.Date >= s.Date);
                if (end is { } e) query = query.Where(m => m.MeasuredAt.Date <= e.Date);
                break;
        }
        var list = query.OrderByDescending(m => m.MeasuredAt).ToList();
        var truncated = list.Count > 30;
        if (truncated) list = list.Take(30).ToList();
        return (list.OrderBy(m => m.MeasuredAt).ToList(), truncated);
    }
}
