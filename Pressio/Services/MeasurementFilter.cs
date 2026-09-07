using System;
using System.Collections.Generic;
using System.Linq;
using Pressio.Models;

namespace Pressio.Services;

// Aplica os filtros da lista de medições (período / medicação / horário / busca).
// Extraído do MainViewModel para ser testável isoladamente.
public static class MeasurementFilter
{
    public static IReadOnlyList<BloodPressureMeasurement> Apply(
        IEnumerable<BloodPressureMeasurement> source,
        string period,
        string medication,
        string timeOfDay,
        string search,
        DateTime today)
    {
        IEnumerable<BloodPressureMeasurement> query = source;

        (DateTime Start, DateTime End)? range = period switch
        {
            "Hoje" => (today.Date, today.Date),
            "Últimos 7 dias" => (today.Date.AddDays(-6), today.Date),
            "Últimos 30 dias" => (today.Date.AddDays(-29), today.Date),
            _ => null
        };
        if (range is { } active)
            query = query.Where(m => m.MeasuredAt.Date >= active.Start && m.MeasuredAt.Date <= active.End);

        query = medication switch
        {
            "Antes da medicação" => query.Where(m => m.MedicationTiming == MedicationTiming.BeforeMedication),
            "Depois da medicação" => query.Where(m => m.MedicationTiming == MedicationTiming.AfterMedication),
            "Não informado" => query.Where(m => m.MedicationTiming == MedicationTiming.NotInformed),
            "Não se aplica" => query.Where(m => m.MedicationTiming == MedicationTiming.NotApplicable),
            _ => query
        };

        query = timeOfDay switch
        {
            "Madrugada" => query.Where(m => m.MeasuredAt.Hour < 6),
            "Manhã" => query.Where(m => m.MeasuredAt.Hour >= 6 && m.MeasuredAt.Hour < 12),
            "Tarde" => query.Where(m => m.MeasuredAt.Hour >= 12 && m.MeasuredAt.Hour < 18),
            "Noite" => query.Where(m => m.MeasuredAt.Hour >= 18),
            _ => query
        };

        var trimmed = search?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed))
            query = query.Where(m => m.Notes?.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ?? false);

        return query.ToList();
    }
}
