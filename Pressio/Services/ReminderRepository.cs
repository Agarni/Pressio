using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Pressio.Models;

namespace Pressio.Services;

public sealed class ReminderRepository
{
    private readonly string _connectionString;

    public ReminderRepository()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pressio");
        Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "pressio.db") }.ToString();
        Initialize();
    }

    public IReadOnlyList<Reminder> GetAll()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Time, Days, Enabled, Note FROM Reminders ORDER BY Time";
        using var reader = command.ExecuteReader();
        var items = new List<Reminder>();
        while (reader.Read())
            items.Add(new Reminder(
                reader.GetInt64(0),
                TimeSpan.Parse(reader.GetString(1)),
                (ReminderDays)reader.GetInt32(2),
                reader.GetInt32(3) != 0,
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        return items;
    }

    public long Add(Reminder reminder)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Reminders (Time, Days, Enabled, Note) VALUES ($time, $days, $enabled, $note); SELECT last_insert_rowid();";
        Bind(command, reminder);
        return (long)(command.ExecuteScalar() ?? 0L);
    }

    public void Update(Reminder reminder)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Reminders SET Time=$time, Days=$days, Enabled=$enabled, Note=$note WHERE Id=$id";
        Bind(command, reminder);
        command.Parameters.AddWithValue("$id", reminder.Id);
        command.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Reminders WHERE Id=$id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open() { var connection = new SqliteConnection(_connectionString); connection.Open(); return connection; }

    private static void Bind(SqliteCommand command, Reminder reminder)
    {
        command.Parameters.AddWithValue("$time", reminder.Time.ToString(@"hh\:mm\:ss"));
        command.Parameters.AddWithValue("$days", (int)reminder.Days);
        command.Parameters.AddWithValue("$enabled", reminder.Enabled ? 1 : 0);
        command.Parameters.AddWithValue("$note", reminder.Note ?? (object)DBNull.Value);
    }

    private void Initialize()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS Reminders (Id INTEGER PRIMARY KEY AUTOINCREMENT, Time TEXT NOT NULL, Days INTEGER NOT NULL, Enabled INTEGER NOT NULL, Note TEXT NULL)";
        command.ExecuteNonQuery();
    }
}
