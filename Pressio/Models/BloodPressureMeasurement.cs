using System;

namespace Pressio.Models;

public enum MedicationTiming
{
    NotInformed,
    BeforeMedication,
    AfterMedication,
    NotApplicable
}

public sealed record BloodPressureMeasurement(
    int Systolic,
    int Diastolic,
    DateTime MeasuredAt,
    MedicationTiming MedicationTiming,
    string? Notes = null,
    MeasurementContext Context = MeasurementContext.None,
    long Id = 0)
{
    public string DisplayValue => $"{Systolic}/{Diastolic}";
    public string DisplayDate => MeasuredAt.ToString("dd/MM/yyyy HH:mm");
    public string DisplayNotes => string.IsNullOrWhiteSpace(Notes) ? "Sem observação" : Notes;
    public bool HasContext => Context != MeasurementContext.None;
    public string DisplayContext => MeasurementContextInfo.Describe(Context);
}
