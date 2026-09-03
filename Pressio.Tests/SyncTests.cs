using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Pressio.Models;
using Pressio.Models.Sync;
using Pressio.Services;
using Xunit;

namespace Pressio.Tests;

public sealed class SyncTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pressio-sync-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            if (File.Exists(_dbPath + suffix)) File.Delete(_dbPath + suffix);
    }

    [Fact]
    public void Migration_AddsSyncColumns_AndBackfillsExistingRows()
    {
        // Cria um banco no formato ANTIGO (sem colunas de sync) e aponta o repositório para ele.
        using (var conn = new SqliteConnection($"Data Source={_dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE Patients (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, BirthDate TEXT NULL, Notes TEXT NULL); CREATE TABLE BloodPressureMeasurements (Id INTEGER PRIMARY KEY AUTOINCREMENT, PatientId INTEGER NOT NULL, Systolic INTEGER NOT NULL, Diastolic INTEGER NOT NULL, MeasuredAtUtc TEXT NOT NULL, MedicationTiming TEXT NOT NULL, Notes TEXT NULL); INSERT INTO Patients (Name) VALUES ('Old'); INSERT INTO BloodPressureMeasurements (PatientId,Systolic,Diastolic,MeasuredAtUtc,MedicationTiming) VALUES (1,130,80,'2026-01-01T08:00:00.0000000Z','NotInformed');";
            cmd.ExecuteNonQuery();
        }

        var repo = new MeasurementRepository(_dbPath);

        var syncPatients = repo.GetSyncPatients().ToList();
        var patient = Assert.Single(syncPatients);
        Assert.Equal("Old", patient.Name);
        Assert.False(string.IsNullOrEmpty(patient.SyncId));

        var measurement = Assert.Single(repo.GetSyncMeasurements().ToList());
        Assert.False(string.IsNullOrEmpty(measurement.SyncId));
        Assert.Equal(patient.SyncId, measurement.PatientSyncId);
        Assert.Equal(130, measurement.Systolic);
        Assert.Equal(80, measurement.Diastolic);
    }

    [Fact]
    public void Add_GeneratesUniqueSyncIds()
    {
        var repo = new MeasurementRepository(_dbPath);
        var patient = repo.GetPatients().Single();
        var secondId = repo.AddPatient("João", null, null);
        repo.Add(new BloodPressureMeasurement(150, 90, DateTime.Now, MedicationTiming.BeforeMedication), patient.Id);
        repo.Add(new BloodPressureMeasurement(120, 80, DateTime.Now, MedicationTiming.AfterMedication), secondId);

        var patients = repo.GetSyncPatients().ToList();
        Assert.Equal(2, patients.Count);
        Assert.Equal(2, patients.Select(x => x.SyncId).Distinct().Count());

        var measurements = repo.GetSyncMeasurements().ToList();
        Assert.Equal(2, measurements.Count);
        Assert.Equal(2, measurements.Select(x => x.SyncId).Distinct().Count());
        var patientSyncIds = patients.Select(x => x.SyncId).ToHashSet();
        Assert.All(measurements, x => Assert.Contains(x.PatientSyncId, patientSyncIds));
    }

    [Fact]
    public void SyncStore_RoundTrips()
    {
        var snapshot = new SyncSnapshot
        {
            FormatVersion = 1,
            DeviceId = "dev-1",
            ExportedAt = new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero),
            Patients = { new SyncPatient { SyncId = "p1", Name = "João", BirthDate = new DateTime(1990, 5, 10), UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) } },
            Measurements = { new SyncMeasurement { SyncId = "m1", PatientSyncId = "p1", Systolic = 130, Diastolic = 80, MeasuredAtUtc = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc), MedicationTiming = MedicationTiming.BeforeMedication, Context = MeasurementContext.Stress, HeartRate = 72, AtRest = true, Arm = Arm.Right, Position = BodyPosition.Seated, UpdatedAt = new DateTimeOffset(2026, 1, 1, 8, 1, 0, TimeSpan.Zero) } },
            Settings = { ["Theme"] = new SyncSetting { Value = "Escuro", UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) } }
        };

        var store = new SyncStore();
        var back = store.Deserialize(store.Serialize(snapshot));

        Assert.Equal(1, back.FormatVersion);
        Assert.Equal("dev-1", back.DeviceId);

        var p = Assert.Single(back.Patients);
        Assert.Equal("p1", p.SyncId);
        Assert.Equal("João", p.Name);
        Assert.Equal(new DateTime(1990, 5, 10), p.BirthDate);

        var m = Assert.Single(back.Measurements);
        Assert.Equal("m1", m.SyncId);
        Assert.Equal("p1", m.PatientSyncId);
        Assert.Equal(130, m.Systolic);
        Assert.Equal(MedicationTiming.BeforeMedication, m.MedicationTiming);
        Assert.Equal(MeasurementContext.Stress, m.Context);
        Assert.Equal(72, m.HeartRate);
        Assert.True(m.AtRest);
        Assert.Equal(Arm.Right, m.Arm);
        Assert.Equal(BodyPosition.Seated, m.Position);

        Assert.Equal("Escuro", back.Settings["Theme"].Value);
    }

    [Fact]
    public void SyncStore_IgnoresUnknownFields_AndToleratesMissing()
    {
        var store = new SyncStore();
        var json = "{\"formatVersion\":99,\"deviceId\":\"x\",\"unknownField\":123,\"patients\":[{\"syncId\":\"p1\",\"name\":\"A\",\"extra\":true}]}";
        var s = store.Deserialize(json);
        Assert.Equal(99, s.FormatVersion);
        Assert.Equal("x", s.DeviceId);
        var p = Assert.Single(s.Patients);
        Assert.Equal("p1", p.SyncId);
        Assert.Equal("A", p.Name);
    }

    [Fact]
    public void SyncService_BuildLocalSnapshot()
    {
        var repository = new MeasurementRepository(_dbPath);
        var reminders = new ReminderRepository(_dbPath);
        var settings = new SettingsRepository(_dbPath);
        var patient = repository.GetPatients().Single();
        repository.Add(new BloodPressureMeasurement(140, 90, DateTime.Now, MedicationTiming.NotInformed), patient.Id);
        reminders.Add(new Reminder(0, new TimeSpan(8, 0, 0), ReminderDays.All, true, "manhã"));

        var deviceId = settings.GetOrCreateSyncDeviceId();
        var service = new SyncService(repository, reminders, settings, deviceId);
        var snapshot = service.BuildLocalSnapshot();

        Assert.Equal(1, snapshot.FormatVersion);
        Assert.Equal(deviceId, snapshot.DeviceId);
        Assert.Single(snapshot.Patients);
        var m = Assert.Single(snapshot.Measurements);
        Assert.Equal(140, m.Systolic);
        Assert.Equal(90, m.Diastolic);
        Assert.Single(snapshot.Reminders);
        Assert.True(snapshot.Settings.ContainsKey("SyncDeviceId"));
    }

    private static SyncService CreateService(string path) =>
        new(new MeasurementRepository(path), new ReminderRepository(path), new SettingsRepository(path), "dev");

    [Fact]
    public void Merge_TakesNewestByUpdatedAt()
    {
        var svc = CreateService(_dbPath);
        var t1 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
        var local = new SyncSnapshot { Patients = { new SyncPatient { SyncId = "p1", Name = "LocalNovo", UpdatedAt = t2 } } };
        var remote = new SyncSnapshot
        {
            Patients =
            {
                new SyncPatient { SyncId = "p1", Name = "RemotoAntigo", UpdatedAt = t1 },
                new SyncPatient { SyncId = "p2", Name = "RemotoUnico", UpdatedAt = t1 }
            }
        };

        var merged = svc.Merge(local, remote);
        Assert.Equal("LocalNovo", merged.Patients.Single(p => p.SyncId == "p1").Name);
        Assert.Contains(merged.Patients, p => p.SyncId == "p2");
    }

    [Fact]
    public void Merge_TombstoneWins()
    {
        var svc = CreateService(_dbPath);
        var t1 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
        var local = new SyncSnapshot { Patients = { new SyncPatient { SyncId = "p1", Name = "João", UpdatedAt = t1 } } };
        var remote = new SyncSnapshot { Patients = { new SyncPatient { SyncId = "p1", Deleted = true, UpdatedAt = t2 } } };

        var merged = svc.Merge(local, remote);
        Assert.True(merged.Patients.Single(p => p.SyncId == "p1").Deleted);
    }

    [Fact]
    public void Apply_AddsAndDeletes()
    {
        var repo = new MeasurementRepository(_dbPath);
        var reminders = new ReminderRepository(_dbPath);
        var settings = new SettingsRepository(_dbPath);
        var svc = new SyncService(repo, reminders, settings, "dev");

        var patientSync = "p-sync-1";
        var measureSync = "m-sync-1";
        var reminderSync = "r-sync-1";
        var now = DateTimeOffset.UtcNow;

        var added = new SyncSnapshot
        {
            Patients = { new SyncPatient { SyncId = patientSync, Name = "João", UpdatedAt = now } },
            Measurements = { new SyncMeasurement { SyncId = measureSync, PatientSyncId = patientSync, Systolic = 150, Diastolic = 95, MeasuredAtUtc = DateTime.UtcNow, MedicationTiming = MedicationTiming.BeforeMedication, UpdatedAt = now } },
            Reminders = { new SyncReminder { SyncId = reminderSync, Time = new TimeSpan(8, 0, 0), Days = ReminderDays.All, Enabled = true, UpdatedAt = now } }
        };
        svc.Apply(added);
        Assert.Contains(repo.GetPatients(), p => p.Name == "João");
        Assert.Contains(repo.GetSyncMeasurements().ToList(), m => m.SyncId == measureSync && !m.Deleted);
        Assert.Single(reminders.GetAll());

        var deleted = new SyncSnapshot
        {
            Patients = { new SyncPatient { SyncId = patientSync, Name = "João", Deleted = true, UpdatedAt = now.AddMinutes(1) } },
            Measurements = { new SyncMeasurement { SyncId = measureSync, PatientSyncId = patientSync, Systolic = 150, Diastolic = 95, MeasuredAtUtc = DateTime.UtcNow, MedicationTiming = MedicationTiming.BeforeMedication, Deleted = true, UpdatedAt = now.AddMinutes(1) } },
            Reminders = { new SyncReminder { SyncId = reminderSync, Time = new TimeSpan(8, 0, 0), Days = ReminderDays.All, Enabled = true, Deleted = true, UpdatedAt = now.AddMinutes(1) } }
        };
        svc.Apply(deleted);
        Assert.DoesNotContain(repo.GetPatients(), p => p.Name == "João");
        Assert.Empty(repo.GetSyncMeasurements().ToList().Where(m => !m.Deleted));
        Assert.Empty(reminders.GetAll());
    }

    [Fact]
    public void ExportImport_TransfersAcrossDevices()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"pressio-json-{Guid.NewGuid():N}.json");
        var dbA = Path.Combine(Path.GetTempPath(), $"pressio-a-{Guid.NewGuid():N}.db");
        var dbB = Path.Combine(Path.GetTempPath(), $"pressio-b-{Guid.NewGuid():N}.db");
        try
        {
            var ma = new MeasurementRepository(dbA);
            var pa = ma.GetPatients().Single();
            ma.AddPatient("João", null, null);
            ma.Add(new BloodPressureMeasurement(160, 100, DateTime.Now, MedicationTiming.NotInformed), pa.Id);
            var svcA = new SyncService(ma, new ReminderRepository(dbA), new SettingsRepository(dbA), "dev-a");
            svcA.ExportToFile(filePath);

            var mb = new MeasurementRepository(dbB);
            var svcB = new SyncService(mb, new ReminderRepository(dbB), new SettingsRepository(dbB), "dev-b");
            svcB.ImportFromFile(filePath);

            Assert.Contains(mb.GetSyncPatients().ToList(), p => p.Name == "João");
            Assert.Contains(mb.GetSyncMeasurements().ToList(), m => m.Systolic == 160);
        }
        finally
        {
            foreach (var f in new[] { filePath, dbA, dbA + "-wal", dbA + "-shm", dbB, dbB + "-wal", dbB + "-shm" })
                if (File.Exists(f)) File.Delete(f);
        }
    }
}
