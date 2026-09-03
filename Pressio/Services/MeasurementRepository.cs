using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Pressio.Models;
using Pressio.Models.Sync;

namespace Pressio.Services;

public sealed class MeasurementRepository
{
    private readonly string _connectionString;

    public MeasurementRepository(string? dbPath = null)
    {
        var dataSource = dbPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pressio", "pressio.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dataSource)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = dataSource }.ToString();
        Initialize();
    }

    public IReadOnlyList<Patient> GetPatients()
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, BirthDate, Notes FROM Patients WHERE Deleted=0 ORDER BY Name";
        using var reader = command.ExecuteReader(); var patients = new List<Patient>();
        while (reader.Read()) patients.Add(new Patient(reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? null : DateTime.Parse(reader.GetString(2)), reader.IsDBNull(3) ? null : reader.GetString(3)));
        return patients;
    }

    public long AddPatient(string name, DateTime? birthDate, string? notes)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Patients (Name, BirthDate, Notes, SyncId, UpdatedAtUtc, Deleted) VALUES ($name, $birth, $notes, $syncId, $updatedAt, 0); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$name", name); command.Parameters.AddWithValue("$birth", birthDate?.ToString("O") ?? (object)DBNull.Value); command.Parameters.AddWithValue("$notes", notes ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$syncId", Guid.NewGuid().ToString("D")); command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O"));
        return (long)(command.ExecuteScalar() ?? 0L);
    }

    public void UpdatePatient(Patient patient)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Patients SET Name=$name, BirthDate=$birth, Notes=$notes, UpdatedAtUtc=$updatedAt WHERE Id=$id";
        command.Parameters.AddWithValue("$name", patient.Name); command.Parameters.AddWithValue("$birth", patient.BirthDate?.ToString("O") ?? (object)DBNull.Value); command.Parameters.AddWithValue("$notes", patient.Notes ?? (object)DBNull.Value); command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O")); command.Parameters.AddWithValue("$id", patient.Id); command.ExecuteNonQuery();
    }

    public void DeletePatient(long id)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "UPDATE BloodPressureMeasurements SET Deleted=1, UpdatedAtUtc=$updatedAt WHERE PatientId=$id; UPDATE Patients SET Deleted=1, UpdatedAtUtc=$updatedAt WHERE Id=$id";
        command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O")); command.ExecuteNonQuery();
    }

    public long Add(BloodPressureMeasurement measurement, long patientId)
    {
        using var connection = Open();
        var patientSyncId = (string?)ScalarString(connection, "SELECT SyncId FROM Patients WHERE Id=$id", patientId) ?? string.Empty;
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO BloodPressureMeasurements (PatientId, PatientSyncId, Systolic, Diastolic, MeasuredAtUtc, MedicationTiming, Notes, Context, HeartRate, AtRest, Arm, Position, SyncId, UpdatedAtUtc, Deleted) VALUES ($patient, $patientSync, $systolic, $diastolic, $measuredAt, $medication, $notes, $context, $heartRate, $atRest, $arm, $position, $syncId, $updatedAt, 0); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$patient", patientId); command.Parameters.AddWithValue("$patientSync", patientSyncId); BindMeasurement(command, measurement);
        command.Parameters.AddWithValue("$syncId", Guid.NewGuid().ToString("D")); command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O"));
        return (long)(command.ExecuteScalar() ?? 0L);
    }

    public IReadOnlyList<BloodPressureMeasurement> GetRecent(long patientId, int limit = 100)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Systolic, Diastolic, MeasuredAtUtc, MedicationTiming, Notes, Context, HeartRate, AtRest, Arm, Position FROM BloodPressureMeasurements WHERE PatientId=$patient AND Deleted=0 ORDER BY MeasuredAtUtc DESC LIMIT $limit";
        command.Parameters.AddWithValue("$patient", patientId); command.Parameters.AddWithValue("$limit", limit);
        using var reader = command.ExecuteReader(); var items = new List<BloodPressureMeasurement>();
        while (reader.Read()) items.Add(new BloodPressureMeasurement(reader.GetInt32(1), reader.GetInt32(2), DateTime.Parse(reader.GetString(3)).ToLocalTime(), Enum.Parse<MedicationTiming>(reader.GetString(4)), reader.IsDBNull(5) ? null : reader.GetString(5), (MeasurementContext)reader.GetInt32(6), reader.IsDBNull(7) ? null : reader.GetInt32(7), reader.GetInt32(8) != 0, reader.IsDBNull(9) ? Arm.NotInformed : Enum.Parse<Arm>(reader.GetString(9)), reader.IsDBNull(10) ? BodyPosition.NotInformed : Enum.Parse<BodyPosition>(reader.GetString(10)), reader.GetInt64(0)));
        return items;
    }

    public void Update(BloodPressureMeasurement measurement)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "UPDATE BloodPressureMeasurements SET Systolic=$systolic, Diastolic=$diastolic, MeasuredAtUtc=$measuredAt, MedicationTiming=$medication, Notes=$notes, Context=$context, HeartRate=$heartRate, AtRest=$atRest, Arm=$arm, Position=$position, UpdatedAtUtc=$updatedAt WHERE Id=$id";
        BindMeasurement(command, measurement); command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O")); command.Parameters.AddWithValue("$id", measurement.Id); command.ExecuteNonQuery();
    }

    public void Delete(long id) { using var connection = Open(); using var command = connection.CreateCommand(); command.CommandText = "UPDATE BloodPressureMeasurements SET Deleted=1, UpdatedAtUtc=$updatedAt WHERE Id=$id"; command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O")); command.Parameters.AddWithValue("$id", id); command.ExecuteNonQuery(); }

    public IReadOnlyList<SyncPatient> GetSyncPatients()
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT SyncId, Name, BirthDate, Notes, UpdatedAtUtc, Deleted FROM Patients";
        using var reader = command.ExecuteReader(); var list = new List<SyncPatient>();
        while (reader.Read()) list.Add(new SyncPatient { SyncId = reader.GetString(0), Name = reader.GetString(1), BirthDate = reader.IsDBNull(2) ? null : DateTime.Parse(reader.GetString(2)), Notes = reader.IsDBNull(3) ? null : reader.GetString(3), UpdatedAt = ParseDateTime(reader, 4), Deleted = reader.GetInt32(5) != 0 });
        return list;
    }

    public IReadOnlyList<SyncMeasurement> GetSyncMeasurements()
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT SyncId, PatientSyncId, Systolic, Diastolic, MeasuredAtUtc, MedicationTiming, Notes, Context, HeartRate, AtRest, Arm, Position, UpdatedAtUtc, Deleted FROM BloodPressureMeasurements";
        using var reader = command.ExecuteReader(); var list = new List<SyncMeasurement>();
        while (reader.Read())
            list.Add(new SyncMeasurement
            {
                SyncId = reader.GetString(0),
                PatientSyncId = reader.GetString(1),
                Systolic = reader.GetInt32(2),
                Diastolic = reader.GetInt32(3),
                MeasuredAtUtc = DateTime.Parse(reader.GetString(4)),
                MedicationTiming = Enum.Parse<MedicationTiming>(reader.GetString(5)),
                Notes = reader.IsDBNull(6) ? null : reader.GetString(6),
                Context = (MeasurementContext)reader.GetInt32(7),
                HeartRate = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                AtRest = reader.GetInt32(9) != 0,
                Arm = reader.IsDBNull(10) ? Arm.NotInformed : Enum.Parse<Arm>(reader.GetString(10)),
                Position = reader.IsDBNull(11) ? BodyPosition.NotInformed : Enum.Parse<BodyPosition>(reader.GetString(11)),
                UpdatedAt = ParseDateTime(reader, 12),
                Deleted = reader.GetInt32(13) != 0
            });
        return list;
    }

    public long? GetPatientIdBySyncId(string syncId)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM Patients WHERE SyncId=$syncId AND Deleted=0";
        command.Parameters.AddWithValue("$syncId", syncId);
        return command.ExecuteScalar() as long?;
    }

    public long UpsertPatient(SyncPatient patient)
    {
        using var connection = Open();
        using var find = connection.CreateCommand();
        find.CommandText = "SELECT Id FROM Patients WHERE SyncId=$syncId";
        find.Parameters.AddWithValue("$syncId", patient.SyncId);
        var existing = find.ExecuteScalar() as long?;
        if (existing is not null)
        {
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE Patients SET Name=$name, BirthDate=$birth, Notes=$notes, SyncId=$syncId, UpdatedAtUtc=$updatedAt, Deleted=0 WHERE Id=$id";
            update.Parameters.AddWithValue("$name", patient.Name); update.Parameters.AddWithValue("$birth", patient.BirthDate?.ToString("O") ?? (object)DBNull.Value); update.Parameters.AddWithValue("$notes", patient.Notes ?? (object)DBNull.Value); update.Parameters.AddWithValue("$syncId", patient.SyncId); update.Parameters.AddWithValue("$updatedAt", patient.UpdatedAt.UtcDateTime.ToString("O")); update.Parameters.AddWithValue("$id", existing.Value);
            update.ExecuteNonQuery();
            return existing.Value;
        }
        using var insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO Patients (Name, BirthDate, Notes, SyncId, UpdatedAtUtc, Deleted) VALUES ($name, $birth, $notes, $syncId, $updatedAt, 0); SELECT last_insert_rowid();";
        insert.Parameters.AddWithValue("$name", patient.Name); insert.Parameters.AddWithValue("$birth", patient.BirthDate?.ToString("O") ?? (object)DBNull.Value); insert.Parameters.AddWithValue("$notes", patient.Notes ?? (object)DBNull.Value); insert.Parameters.AddWithValue("$syncId", patient.SyncId); insert.Parameters.AddWithValue("$updatedAt", patient.UpdatedAt.UtcDateTime.ToString("O"));
        return (long)(insert.ExecuteScalar() ?? 0L);
    }

    public void UpsertMeasurement(SyncMeasurement measurement, long patientId)
    {
        using var connection = Open();
        using var find = connection.CreateCommand();
        find.CommandText = "SELECT Id FROM BloodPressureMeasurements WHERE SyncId=$syncId";
        find.Parameters.AddWithValue("$syncId", measurement.SyncId);
        var existing = find.ExecuteScalar() as long?;
        if (existing is not null)
        {
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE BloodPressureMeasurements SET PatientId=$patient, PatientSyncId=$patientSync, Systolic=$systolic, Diastolic=$diastolic, MeasuredAtUtc=$measuredAt, MedicationTiming=$medication, Notes=$notes, Context=$context, HeartRate=$heartRate, AtRest=$atRest, Arm=$arm, Position=$position, SyncId=$syncId, UpdatedAtUtc=$updatedAt, Deleted=0 WHERE Id=$id";
            BindMeasurement(update, new BloodPressureMeasurement(measurement.Systolic, measurement.Diastolic, measurement.MeasuredAtUtc.ToLocalTime(), measurement.MedicationTiming, measurement.Notes, measurement.Context, measurement.HeartRate, measurement.AtRest, measurement.Arm, measurement.Position));
            update.Parameters.AddWithValue("$patient", patientId); update.Parameters.AddWithValue("$patientSync", measurement.PatientSyncId); update.Parameters.AddWithValue("$syncId", measurement.SyncId); update.Parameters.AddWithValue("$updatedAt", measurement.UpdatedAt.UtcDateTime.ToString("O")); update.Parameters.AddWithValue("$id", existing.Value);
            update.ExecuteNonQuery();
        }
        else
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO BloodPressureMeasurements (PatientId, PatientSyncId, Systolic, Diastolic, MeasuredAtUtc, MedicationTiming, Notes, Context, HeartRate, AtRest, Arm, Position, SyncId, UpdatedAtUtc, Deleted) VALUES ($patient, $patientSync, $systolic, $diastolic, $measuredAt, $medication, $notes, $context, $heartRate, $atRest, $arm, $position, $syncId, $updatedAt, 0); SELECT last_insert_rowid();";
            BindMeasurement(insert, new BloodPressureMeasurement(measurement.Systolic, measurement.Diastolic, measurement.MeasuredAtUtc.ToLocalTime(), measurement.MedicationTiming, measurement.Notes, measurement.Context, measurement.HeartRate, measurement.AtRest, measurement.Arm, measurement.Position));
            insert.Parameters.AddWithValue("$patient", patientId); insert.Parameters.AddWithValue("$patientSync", measurement.PatientSyncId); insert.Parameters.AddWithValue("$syncId", measurement.SyncId); insert.Parameters.AddWithValue("$updatedAt", measurement.UpdatedAt.UtcDateTime.ToString("O"));
            insert.ExecuteNonQuery();
        }
    }

    public void DeletePatientBySyncId(string syncId)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "UPDATE BloodPressureMeasurements SET Deleted=1, UpdatedAtUtc=$updatedAt WHERE PatientSyncId=$syncId; UPDATE Patients SET Deleted=1, UpdatedAtUtc=$updatedAt WHERE SyncId=$syncId";
        command.Parameters.AddWithValue("$syncId", syncId); command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O")); command.ExecuteNonQuery();
    }

    public void DeleteMeasurementBySyncId(string syncId)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "UPDATE BloodPressureMeasurements SET Deleted=1, UpdatedAtUtc=$updatedAt WHERE SyncId=$syncId";
        command.Parameters.AddWithValue("$syncId", syncId); command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O")); command.ExecuteNonQuery();
    }

    private SqliteConnection Open() { var connection = new SqliteConnection(_connectionString); connection.Open(); return connection; }

    private static object? ScalarString(SqliteConnection connection, string sql, long id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql; command.Parameters.AddWithValue("$id", id);
        return command.ExecuteScalar();
    }

    private static DateTimeOffset ParseDateTime(SqliteDataReader reader, int ordinal) =>
        DateTimeOffset.Parse(reader.GetString(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    private static void BindMeasurement(SqliteCommand command, BloodPressureMeasurement m) { command.Parameters.AddWithValue("$systolic", m.Systolic); command.Parameters.AddWithValue("$diastolic", m.Diastolic); command.Parameters.AddWithValue("$measuredAt", m.MeasuredAt.ToUniversalTime().ToString("O")); command.Parameters.AddWithValue("$medication", m.MedicationTiming.ToString()); command.Parameters.AddWithValue("$notes", m.Notes ?? (object)DBNull.Value); command.Parameters.AddWithValue("$context", (int)m.Context); command.Parameters.AddWithValue("$heartRate", m.HeartRate is { } hr ? hr : (object)DBNull.Value); command.Parameters.AddWithValue("$atRest", m.AtRest ? 1 : 0); command.Parameters.AddWithValue("$arm", m.Arm.ToString()); command.Parameters.AddWithValue("$position", m.Position.ToString()); }

    private static void EnsureColumn(SqliteConnection connection, string table, string column, string definition)
    {
        var found = false;
        using var schema = connection.CreateCommand();
        schema.CommandText = $"PRAGMA table_info({table});";
        using var reader = schema.ExecuteReader();
        while (reader.Read()) if (reader.GetString(1) == column) { found = true; break; }
        if (!found) { using var alter = connection.CreateCommand(); alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}"; alter.ExecuteNonQuery(); }
    }

    private void Initialize()
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS Patients (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, BirthDate TEXT NULL, Notes TEXT NULL); CREATE TABLE IF NOT EXISTS BloodPressureMeasurements (Id INTEGER PRIMARY KEY AUTOINCREMENT, PatientId INTEGER NOT NULL DEFAULT 1, Systolic INTEGER NOT NULL, Diastolic INTEGER NOT NULL, MeasuredAtUtc TEXT NOT NULL, MedicationTiming TEXT NOT NULL, Notes TEXT NULL);";
        command.ExecuteNonQuery();
        using var schema = connection.CreateCommand(); schema.CommandText = "PRAGMA table_info(BloodPressureMeasurements);";
        using var reader = schema.ExecuteReader(); var hasPatientId = false; var hasContext = false; var hasHeartRate = false; var hasAtRest = false; var hasArm = false; var hasPosition = false;
        while (reader.Read()) { var name = reader.GetString(1); if (name == "PatientId") hasPatientId = true; else if (name == "Context") hasContext = true; else if (name == "HeartRate") hasHeartRate = true; else if (name == "AtRest") hasAtRest = true; else if (name == "Arm") hasArm = true; else if (name == "Position") hasPosition = true; }
        reader.Close();
        if (!hasPatientId) { using var migration = connection.CreateCommand(); migration.CommandText = "ALTER TABLE BloodPressureMeasurements ADD COLUMN PatientId INTEGER NOT NULL DEFAULT 1"; migration.ExecuteNonQuery(); }
        if (!hasContext) { using var migration = connection.CreateCommand(); migration.CommandText = "ALTER TABLE BloodPressureMeasurements ADD COLUMN Context INTEGER NOT NULL DEFAULT 0"; migration.ExecuteNonQuery(); }
        if (!hasHeartRate) { using var migration = connection.CreateCommand(); migration.CommandText = "ALTER TABLE BloodPressureMeasurements ADD COLUMN HeartRate INTEGER NULL"; migration.ExecuteNonQuery(); }
        if (!hasAtRest) { using var migration = connection.CreateCommand(); migration.CommandText = "ALTER TABLE BloodPressureMeasurements ADD COLUMN AtRest INTEGER NOT NULL DEFAULT 0"; migration.ExecuteNonQuery(); }
        if (!hasArm) { using var migration = connection.CreateCommand(); migration.CommandText = "ALTER TABLE BloodPressureMeasurements ADD COLUMN Arm TEXT NOT NULL DEFAULT 'NotInformed'"; migration.ExecuteNonQuery(); }
        if (!hasPosition) { using var migration = connection.CreateCommand(); migration.CommandText = "ALTER TABLE BloodPressureMeasurements ADD COLUMN Position TEXT NOT NULL DEFAULT 'NotInformed'"; migration.ExecuteNonQuery(); }

        EnsureColumn(connection, "Patients", "SyncId", "TEXT NULL");
        EnsureColumn(connection, "Patients", "UpdatedAtUtc", "TEXT NULL");
        EnsureColumn(connection, "Patients", "Deleted", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "BloodPressureMeasurements", "SyncId", "TEXT NULL");
        EnsureColumn(connection, "BloodPressureMeasurements", "PatientSyncId", "TEXT NULL");
        EnsureColumn(connection, "BloodPressureMeasurements", "UpdatedAtUtc", "TEXT NULL");
        EnsureColumn(connection, "BloodPressureMeasurements", "Deleted", "INTEGER NOT NULL DEFAULT 0");

        using var backfill = connection.CreateCommand();
        backfill.CommandText = @"UPDATE Patients SET SyncId = lower(hex(randomblob(16))) WHERE SyncId IS NULL OR SyncId = '';
UPDATE Patients SET UpdatedAtUtc = strftime('%Y-%m-%dT%H:%M:%fZ','now') WHERE UpdatedAtUtc IS NULL OR UpdatedAtUtc = '';
UPDATE BloodPressureMeasurements SET SyncId = lower(hex(randomblob(16))) WHERE SyncId IS NULL OR SyncId = '';
UPDATE BloodPressureMeasurements SET PatientSyncId = (SELECT SyncId FROM Patients WHERE Patients.Id = BloodPressureMeasurements.PatientId) WHERE PatientSyncId IS NULL OR PatientSyncId = '';
UPDATE BloodPressureMeasurements SET UpdatedAtUtc = strftime('%Y-%m-%dT%H:%M:%fZ','now') WHERE UpdatedAtUtc IS NULL OR UpdatedAtUtc = '';";
        backfill.ExecuteNonQuery();

        using var count = connection.CreateCommand(); count.CommandText = "SELECT COUNT(*) FROM Patients WHERE Deleted=0";
        if (Convert.ToInt64(count.ExecuteScalar()) == 0) AddPatient("Meu perfil", null, null);
    }
}
