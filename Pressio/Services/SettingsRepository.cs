using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Pressio.Services;

public sealed class SettingsRepository
{
    private readonly string _connectionString;

    public SettingsRepository()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pressio");
        Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "pressio.db") }.ToString();
        Initialize();
    }

    public string GetAppearance() => Get("Appearance", "Claro");
    public string GetPrimaryColor() => Get("PrimaryColor", "Índigo");
    public string GetMeasurementDisplayFormat() => Get("MeasurementDisplayFormat", "13/8");
    public void SaveAppearance(string appearance, string primaryColor)
    {
        Set("Appearance", appearance);
        Set("PrimaryColor", primaryColor);
    }
    public void SaveMeasurementDisplayFormat(string format) => Set("MeasurementDisplayFormat", format);

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
