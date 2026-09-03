using System;
using System.Linq;
using Pressio.Models.Sync;

namespace Pressio.Services;

/// <summary>Monta o snapshot local de sincronia (fonte de verdade = banco SQLite local).</summary>
public sealed class SyncService
{
    private readonly MeasurementRepository _measurements;
    private readonly ReminderRepository _reminders;
    private readonly SettingsRepository _settings;
    private readonly string _deviceId;

    public SyncService(MeasurementRepository measurements, ReminderRepository reminders, SettingsRepository settings, string deviceId)
    {
        _measurements = measurements;
        _reminders = reminders;
        _settings = settings;
        _deviceId = deviceId;
    }

    public SyncSnapshot BuildLocalSnapshot() => new()
    {
        FormatVersion = 1,
        DeviceId = _deviceId,
        ExportedAt = DateTimeOffset.UtcNow,
        Patients = _measurements.GetSyncPatients().ToList(),
        Measurements = _measurements.GetSyncMeasurements().ToList(),
        Reminders = _reminders.GetSyncReminders().ToList(),
        Settings = _settings.GetSyncSettings()
    };
}
