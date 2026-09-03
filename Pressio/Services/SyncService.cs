using System;
using System.Collections.Generic;
using System.Linq;
using Pressio.Models.Sync;

namespace Pressio.Services;

/// <summary>Motor de sincronia: monta o snapshot local, mescla com o remoto (LWW por syncId/updatedAt) e aplica no banco.</summary>
public sealed class SyncService
{
    private readonly MeasurementRepository _measurements;
    private readonly ReminderRepository _reminders;
    private readonly SettingsRepository _settings;
    private readonly SyncStore _store;
    private readonly string _deviceId;

    public SyncService(MeasurementRepository measurements, ReminderRepository reminders, SettingsRepository settings, string deviceId)
    {
        _measurements = measurements;
        _reminders = reminders;
        _settings = settings;
        _store = new SyncStore();
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

    /// <summary>Mescla o local com o remoto: vence o registro de <c>UpdatedAt</c> mais recente (tie -> local).</summary>
    public SyncSnapshot Merge(SyncSnapshot local, SyncSnapshot remote) => new()
    {
        FormatVersion = Math.Max(local.FormatVersion, remote.FormatVersion),
        DeviceId = _deviceId,
        ExportedAt = DateTimeOffset.UtcNow,
        Patients = MergeBySyncId(local.Patients, remote.Patients, p => p.SyncId, p => p.UpdatedAt),
        Measurements = MergeBySyncId(local.Measurements, remote.Measurements, m => m.SyncId, m => m.UpdatedAt),
        Reminders = MergeBySyncId(local.Reminders, remote.Reminders, r => r.SyncId, r => r.UpdatedAt),
        Settings = MergeSettings(local.Settings, remote.Settings)
    };

    private static List<T> MergeBySyncId<T>(IReadOnlyList<T> local, IReadOnlyList<T> remote, Func<T, string> key, Func<T, DateTimeOffset> stamp)
    {
        var byId = new Dictionary<string, T>();
        foreach (var item in remote) byId[key(item)] = item;
        foreach (var item in local)
        {
            var k = key(item);
            if (byId.TryGetValue(k, out var existing) && stamp(existing) > stamp(item)) continue;
            byId[k] = item;
        }
        return byId.Values.ToList();
    }

    private static Dictionary<string, SyncSetting> MergeSettings(Dictionary<string, SyncSetting> local, Dictionary<string, SyncSetting> remote)
    {
        var result = new Dictionary<string, SyncSetting>(remote);
        foreach (var kv in local)
            if (!result.TryGetValue(kv.Key, out var existing) || existing.UpdatedAt <= kv.Value.UpdatedAt)
                result[kv.Key] = kv.Value;
        return result;
    }

    /// <summary>Aplica o snapshot mesclado ao banco local (SQLite = fonte de verdade).</summary>
    public void Apply(SyncSnapshot merged)
    {
        var patientIdBySyncId = new Dictionary<string, long>();
        foreach (var patient in merged.Patients)
        {
            if (patient.Deleted) { _measurements.DeletePatientBySyncId(patient.SyncId); continue; }
            patientIdBySyncId[patient.SyncId] = _measurements.UpsertPatient(patient);
        }

        foreach (var m in merged.Measurements)
        {
            if (m.Deleted) { _measurements.DeleteMeasurementBySyncId(m.SyncId); continue; }
            var patientId = patientIdBySyncId.TryGetValue(m.PatientSyncId, out var id)
                ? id
                : _measurements.GetPatientIdBySyncId(m.PatientSyncId);
            if (patientId is null) continue;
            _measurements.UpsertMeasurement(m, patientId.Value);
        }

        foreach (var reminder in merged.Reminders)
        {
            if (reminder.Deleted) { _reminders.DeleteReminderBySyncId(reminder.SyncId); continue; }
            _reminders.UpsertReminder(reminder);
        }

        foreach (var setting in merged.Settings)
            _settings.SetSync(setting.Key, setting.Value.Value, setting.Value.UpdatedAt);
    }

    /// <summary>Exporta o snapshot local para um arquivo JSON.</summary>
    public void ExportToFile(string path) => _store.WriteToFile(path, BuildLocalSnapshot());

    /// <summary>Lê um arquivo, mescla com o local, aplica no banco e grava o resultado no arquivo.</summary>
    public SyncSnapshot ImportFromFile(string path)
    {
        var remote = _store.ReadFromFile(path) ?? new SyncSnapshot();
        var merged = Merge(BuildLocalSnapshot(), remote);
        _store.WriteToFile(path, merged);
        Apply(merged);
        return merged;
    }
}
