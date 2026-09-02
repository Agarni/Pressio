using System;
using System.Collections.Generic;
using System.Linq;

namespace Pressio.Models;

[Flags]
public enum MeasurementContext
{
    None = 0,
    Stress = 1 << 0,
    Pain = 1 << 1,
    Fever = 1 << 2,
    RecentExercise = 1 << 3,
    Caffeine = 1 << 4,
    Alcohol = 1 << 5,
    Smoking = 1 << 6,
    PoorSleep = 1 << 7,
    MissedMedication = 1 << 8,
    DifferentDiet = 1 << 9,
    Symptoms = 1 << 10,
    Other = 1 << 11,
}

public static class MeasurementContextInfo
{
    private static readonly (MeasurementContext Value, string Label)[] All = new[]
    {
        (MeasurementContext.Stress, "Estresse ou ansiedade"),
        (MeasurementContext.Pain, "Dor"),
        (MeasurementContext.Fever, "Febre ou mal-estar"),
        (MeasurementContext.RecentExercise, "Atividade física recente"),
        (MeasurementContext.Caffeine, "Café ou energético"),
        (MeasurementContext.Alcohol, "Álcool"),
        (MeasurementContext.Smoking, "Tabagismo recente"),
        (MeasurementContext.PoorSleep, "Sono insuficiente"),
        (MeasurementContext.MissedMedication, "Atraso ou esquecimento da medicação"),
        (MeasurementContext.DifferentDiet, "Alimentação diferente do habitual"),
        (MeasurementContext.Symptoms, "Sintomas percebidos"),
        (MeasurementContext.Other, "Outro fator personalizado"),
    };

    public static IReadOnlyList<(MeasurementContext Value, string Label)> AllContexts => All;

    public static string Describe(MeasurementContext context) => context == MeasurementContext.None
        ? "Sem contexto"
        : string.Join(", ", All.Where(x => (context & x.Value) != 0).Select(x => x.Label));
}
