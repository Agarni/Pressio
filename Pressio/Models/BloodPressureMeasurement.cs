using System;
using System.Linq;

namespace Pressio.Models;

public enum MedicationTiming
{
    NotInformed,
    BeforeMedication,
    AfterMedication,
    NotApplicable
}

public enum Arm
{
    NotInformed,
    Right,
    Left
}

public enum BodyPosition
{
    NotInformed,
    Seated,
    Lying,
    Standing
}

public sealed record BloodPressureMeasurement(
    int Systolic,
    int Diastolic,
    DateTime MeasuredAt,
    MedicationTiming MedicationTiming,
    string? Notes = null,
    MeasurementContext Context = MeasurementContext.None,
    int? HeartRate = null,
    bool AtRest = false,
    Arm Arm = Arm.NotInformed,
    BodyPosition Position = BodyPosition.NotInformed,
    long Id = 0)
{
    public static bool UseShorthandFormat { get; set; } = true;

    public static string Format(int systolic, int diastolic) => UseShorthandFormat
        ? $"{systolic / 10d:0.#}/{diastolic / 10d:0.#}"
        : $"{systolic}/{diastolic}";

    public string DisplayValue => Format(Systolic, Diastolic);
    public PressureCategory Category => BloodPressureClassification.Classify(Systolic, Diastolic);
    public string CategoryLabel => BloodPressureClassification.Label(Category);
    public string DisplayDate => MeasuredAt.ToString("dd/MM/yyyy HH:mm");
    public string DisplayNotes => string.IsNullOrWhiteSpace(Notes) ? "Sem observação" : Notes;
    public bool HasContext => Context != MeasurementContext.None;
    public string DisplayContext => MeasurementContextInfo.Describe(Context);
    public bool HasExtra => !string.IsNullOrEmpty(DisplayExtra);
    public string DisplayExtra => string.Join(" · ", new[]
    {
        HeartRate is { } hr ? $"{hr} bpm" : null,
        AtRest ? "em repouso" : null,
        Arm == Arm.Right ? "braço direito" : Arm == Arm.Left ? "braço esquerdo" : null,
        Position == BodyPosition.Seated ? "sentado" : Position == BodyPosition.Lying ? "deitado" : Position == BodyPosition.Standing ? "em pé" : null,
    }.Where(x => x is not null));
}
