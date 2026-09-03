using System;
using System.Collections.Generic;

namespace Pressio.Models.Sync;

/// <summary>Snapshot completo usado para sincronização entre dispositivos.</summary>
public sealed class SyncSnapshot
{
    public int FormatVersion { get; set; } = 1;
    public string DeviceId { get; set; } = string.Empty;
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<SyncPatient> Patients { get; set; } = new();
    public List<SyncMeasurement> Measurements { get; set; } = new();
    public List<SyncReminder> Reminders { get; set; } = new();
    public Dictionary<string, SyncSetting> Settings { get; set; } = new();
}

public sealed class SyncPatient
{
    public string SyncId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Deleted { get; set; }
    public string DeviceId { get; set; } = string.Empty;
}

public sealed class SyncMeasurement
{
    public string SyncId { get; set; } = string.Empty;
    public string PatientSyncId { get; set; } = string.Empty;
    public int Systolic { get; set; }
    public int Diastolic { get; set; }
    public DateTime MeasuredAtUtc { get; set; }
    public MedicationTiming MedicationTiming { get; set; }
    public string? Notes { get; set; }
    public MeasurementContext Context { get; set; }
    public int? HeartRate { get; set; }
    public bool AtRest { get; set; }
    public Arm Arm { get; set; }
    public BodyPosition Position { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Deleted { get; set; }
    public string DeviceId { get; set; } = string.Empty;
}

public sealed class SyncReminder
{
    public string SyncId { get; set; } = string.Empty;
    public TimeSpan Time { get; set; }
    public ReminderDays Days { get; set; }
    public bool Enabled { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool Deleted { get; set; }
    public string DeviceId { get; set; } = string.Empty;
}

public sealed class SyncSetting
{
    public string Value { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
    public string DeviceId { get; set; } = string.Empty;
}
