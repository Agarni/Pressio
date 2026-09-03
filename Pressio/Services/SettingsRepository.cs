using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Pressio.Models.Sync;

namespace Pressio.Services;

public sealed class SettingsRepository
{
    private readonly string _connectionString;

    public SettingsRepository(string? dbPath = null)
    {
        var dataSource = dbPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pressio", "pressio.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dataSource)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = dataSource }.ToString();
        Initialize();
    }

    public string GetAppearance() => Get("Appearance", "Claro");
    public string GetPrimaryColor() => Get("PrimaryColor", "Índigo");
    public string GetMeasurementDisplayFormat() => Get("MeasurementDisplayFormat", "13/8");
    public string? GetLastExportDirectory() => Get("LastExportDirectory", string.Empty) is { Length: > 0 } dir ? dir : null;
    public void SaveAppearance(string appearance, string primaryColor)
    {
        Set("Appearance", appearance);
        Set("PrimaryColor", primaryColor);
    }
    public void SaveMeasurementDisplayFormat(string format) => Set("MeasurementDisplayFormat", format);
    public void SaveLastExportDirectory(string directory) => Set("LastExportDirectory", directory);

    private string Get(string key, string defaultValue)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key=$key";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string ?? defaultValue;
    }

    private void Set(string key, string value)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO Settings (Key, Value, UpdatedAtUtc) VALUES ($key, $value, $updatedAt)";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public Dictionary<string, SyncSetting> GetSyncSettings()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Key, Value, UpdatedAtUtc FROM Settings";
        using var reader = command.ExecuteReader();
        var result = new Dictionary<string, SyncSetting>();
        while (reader.Read())
            result[reader.GetString(0)] = new SyncSetting
            {
                Value = reader.GetString(1),
                UpdatedAt = reader.IsDBNull(2) ? DateTimeOffset.UtcNow : DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture)
            };
        return result;
    }

    public string GetOrCreateSyncDeviceId()
    {
        var existing = Get("SyncDeviceId", string.Empty);
        if (existing.Length > 0) return existing;
        var id = Guid.NewGuid().ToString("D");
        Set("SyncDeviceId", id);
        return id;
    }

    public void SetSync(string key, string value, DateTimeOffset updatedAt)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO Settings (Key, Value, UpdatedAtUtc) VALUES ($key, $value, $updatedAt)";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$updatedAt", updatedAt.UtcDateTime.ToString("O"));
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open() { var connection = new SqliteConnection(_connectionString); connection.Open(); return connection; }

    private void Initialize()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS Settings (Key TEXT PRIMARY KEY, Value TEXT)";
        command.ExecuteNonQuery();
        var found = false;
        using var schema = connection.CreateCommand();
        schema.CommandText = "PRAGMA table_info(Settings);";
        using var reader = schema.ExecuteReader();
        while (reader.Read()) if (reader.GetString(1) == "UpdatedAtUtc") { found = true; break; }
        if (!found) { using var alter = connection.CreateCommand(); alter.CommandText = "ALTER TABLE Settings ADD COLUMN UpdatedAtUtc TEXT NULL"; alter.ExecuteNonQuery(); }
    }
}
