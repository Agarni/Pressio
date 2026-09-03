using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Pressio.Models;

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
        command.CommandText = "SELECT Id, Name, BirthDate, Notes FROM Patients ORDER BY Name";
        using var reader = command.ExecuteReader(); var patients = new List<Patient>();
        while (reader.Read()) patients.Add(new Patient(reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? null : DateTime.Parse(reader.GetString(2)), reader.IsDBNull(3) ? null : reader.GetString(3)));
        return patients;
    }

    public long AddPatient(string name, DateTime? birthDate, string? notes)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Patients (Name, BirthDate, Notes) VALUES ($name, $birth, $notes); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$name", name); command.Parameters.AddWithValue("$birth", birthDate?.ToString("O") ?? (object)DBNull.Value); command.Parameters.AddWithValue("$notes", notes ?? (object)DBNull.Value);
        return (long)(command.ExecuteScalar() ?? 0L);
    }

    public void UpdatePatient(Patient patient)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Patients SET Name=$name, BirthDate=$birth, Notes=$notes WHERE Id=$id";
        command.Parameters.AddWithValue("$name", patient.Name); command.Parameters.AddWithValue("$birth", patient.BirthDate?.ToString("O") ?? (object)DBNull.Value); command.Parameters.AddWithValue("$notes", patient.Notes ?? (object)DBNull.Value); command.Parameters.AddWithValue("$id", patient.Id); command.ExecuteNonQuery();
    }

    public void DeletePatient(long id)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM BloodPressureMeasurements WHERE PatientId=$id; DELETE FROM Patients WHERE Id=$id";
        command.Parameters.AddWithValue("$id", id); command.ExecuteNonQuery();
    }

    public long Add(BloodPressureMeasurement measurement, long patientId)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO BloodPressureMeasurements (PatientId, Systolic, Diastolic, MeasuredAtUtc, MedicationTiming, Notes, Context, HeartRate, AtRest, Arm, Position) VALUES ($patient, $systolic, $diastolic, $measuredAt, $medication, $notes, $context, $heartRate, $atRest, $arm, $position); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$patient", patientId); BindMeasurement(command, measurement);
        return (long)(command.ExecuteScalar() ?? 0L);
    }

    public IReadOnlyList<BloodPressureMeasurement> GetRecent(long patientId, int limit = 100)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Systolic, Diastolic, MeasuredAtUtc, MedicationTiming, Notes, Context, HeartRate, AtRest, Arm, Position FROM BloodPressureMeasurements WHERE PatientId=$patient ORDER BY MeasuredAtUtc DESC LIMIT $limit";
        command.Parameters.AddWithValue("$patient", patientId); command.Parameters.AddWithValue("$limit", limit);
        using var reader = command.ExecuteReader(); var items = new List<BloodPressureMeasurement>();
        while (reader.Read()) items.Add(new BloodPressureMeasurement(reader.GetInt32(1), reader.GetInt32(2), DateTime.Parse(reader.GetString(3)).ToLocalTime(), Enum.Parse<MedicationTiming>(reader.GetString(4)), reader.IsDBNull(5) ? null : reader.GetString(5), (MeasurementContext)reader.GetInt32(6), reader.IsDBNull(7) ? null : reader.GetInt32(7), reader.GetInt32(8) != 0, reader.IsDBNull(9) ? Arm.NotInformed : Enum.Parse<Arm>(reader.GetString(9)), reader.IsDBNull(10) ? BodyPosition.NotInformed : Enum.Parse<BodyPosition>(reader.GetString(10)), reader.GetInt64(0)));
        return items;
    }

    public void Update(BloodPressureMeasurement measurement)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "UPDATE BloodPressureMeasurements SET Systolic=$systolic, Diastolic=$diastolic, MeasuredAtUtc=$measuredAt, MedicationTiming=$medication, Notes=$notes, Context=$context, HeartRate=$heartRate, AtRest=$atRest, Arm=$arm, Position=$position WHERE Id=$id";
        BindMeasurement(command, measurement); command.Parameters.AddWithValue("$id", measurement.Id); command.ExecuteNonQuery();
    }

    public void Delete(long id) { using var connection = Open(); using var command = connection.CreateCommand(); command.CommandText = "DELETE FROM BloodPressureMeasurements WHERE Id=$id"; command.Parameters.AddWithValue("$id", id); command.ExecuteNonQuery(); }
    private SqliteConnection Open() { var connection = new SqliteConnection(_connectionString); connection.Open(); return connection; }
    private static void BindMeasurement(SqliteCommand command, BloodPressureMeasurement m) { command.Parameters.AddWithValue("$systolic", m.Systolic); command.Parameters.AddWithValue("$diastolic", m.Diastolic); command.Parameters.AddWithValue("$measuredAt", m.MeasuredAt.ToUniversalTime().ToString("O")); command.Parameters.AddWithValue("$medication", m.MedicationTiming.ToString()); command.Parameters.AddWithValue("$notes", m.Notes ?? (object)DBNull.Value); command.Parameters.AddWithValue("$context", (int)m.Context); command.Parameters.AddWithValue("$heartRate", m.HeartRate is { } hr ? hr : (object)DBNull.Value); command.Parameters.AddWithValue("$atRest", m.AtRest ? 1 : 0); command.Parameters.AddWithValue("$arm", m.Arm.ToString()); command.Parameters.AddWithValue("$position", m.Position.ToString()); }
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
        using var count = connection.CreateCommand(); count.CommandText = "SELECT COUNT(*) FROM Patients";
        if (Convert.ToInt64(count.ExecuteScalar()) == 0) AddPatient("Meu perfil", null, null);
    }
}
