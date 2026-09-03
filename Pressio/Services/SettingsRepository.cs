using System;
using System.IO;
using Microsoft.Data.Sqlite;

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
        command.CommandText = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES ($key, $value)";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open() { var connection = new SqliteConnection(_connectionString); connection.Open(); return connection; }

    private void Initialize()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS Settings (Key TEXT PRIMARY KEY, Value TEXT)";
        command.ExecuteNonQuery();
    }
}
