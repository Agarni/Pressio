using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Pressio.Models;
using Pressio.Models.Sync;

namespace Pressio.Services;

public sealed class ReminderRepository
{
    private readonly string _connectionString;

    public ReminderRepository(string? dbPath = null)
    {
        var dataSource = dbPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pressio", "pressio.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dataSource)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = dataSource }.ToString();
        Initialize();
    }

    public IReadOnlyList<Reminder> GetAll()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Time, Days, Enabled, Note FROM Reminders WHERE Deleted=0 ORDER BY Time";
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
        command.CommandText = "INSERT INTO Reminders (Time, Days, Enabled, Note, SyncId, UpdatedAtUtc) VALUES ($time, $days, $enabled, $note, $syncId, $updatedAt); SELECT last_insert_rowid();";
        Bind(command, reminder);
        command.Parameters.AddWithValue("$syncId", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O"));
        return (long)(command.ExecuteScalar() ?? 0L);
    }

    public void Update(Reminder reminder)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Reminders SET Time=$time, Days=$days, Enabled=$enabled, Note=$note, UpdatedAtUtc=$updatedAt WHERE Id=$id";
        Bind(command, reminder);
        command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", reminder.Id);
        command.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Reminders SET Deleted=1, UpdatedAtUtc=$updatedAt WHERE Id=$id";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<SyncReminder> GetSyncReminders()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT SyncId, Time, Days, Enabled, Note, UpdatedAtUtc, Deleted FROM Reminders";
        using var reader = command.ExecuteReader();
        var items = new List<SyncReminder>();
        while (reader.Read())
            items.Add(new SyncReminder
            {
                SyncId = reader.GetString(0),
                Time = TimeSpan.Parse(reader.GetString(1)),
                Days = (ReminderDays)reader.GetInt32(2),
                Enabled = reader.GetInt32(3) != 0,
                Note = reader.IsDBNull(4) ? null : reader.GetString(4),
                UpdatedAt = DateTimeOffset.Parse(reader.GetString(5), System.Globalization.CultureInfo.InvariantCulture),
                Deleted = reader.GetInt32(6) != 0
            });
        return items;
    }

    public void DeleteReminderBySyncId(string syncId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Reminders SET Deleted=1, UpdatedAtUtc=$updatedAt WHERE SyncId=$syncId";
        command.Parameters.AddWithValue("$syncId", syncId);
        command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public int CompactTombstones(int olderThanDays = 30)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Reminders WHERE Deleted=1 AND UpdatedAtUtc < $cutoff";
        command.Parameters.AddWithValue("$cutoff", DateTime.UtcNow.AddDays(-olderThanDays).ToString("O"));
        return command.ExecuteNonQuery();
    }

    public void UpsertReminder(SyncReminder reminder)
    {
        using var connection = Open();
        using var find = connection.CreateCommand();
        find.CommandText = "SELECT Id FROM Reminders WHERE SyncId=$syncId";
        find.Parameters.AddWithValue("$syncId", reminder.SyncId);
        var existing = find.ExecuteScalar() as long?;
        if (existing is not null)
        {
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE Reminders SET Time=$time, Days=$days, Enabled=$enabled, Note=$note, SyncId=$syncId, UpdatedAtUtc=$updatedAt, Deleted=0 WHERE Id=$id";
            Bind(update, new Reminder(existing.Value, reminder.Time, reminder.Days, reminder.Enabled, reminder.Note));
            update.Parameters.AddWithValue("$syncId", reminder.SyncId);
            update.Parameters.AddWithValue("$updatedAt", reminder.UpdatedAt.UtcDateTime.ToString("O"));
            update.Parameters.AddWithValue("$id", existing.Value);
            update.ExecuteNonQuery();
        }
        else
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO Reminders (Time, Days, Enabled, Note, SyncId, UpdatedAtUtc, Deleted) VALUES ($time, $days, $enabled, $note, $syncId, $updatedAt, 0); SELECT last_insert_rowid();";
            Bind(insert, new Reminder(0, reminder.Time, reminder.Days, reminder.Enabled, reminder.Note));
            insert.Parameters.AddWithValue("$syncId", reminder.SyncId);
            insert.Parameters.AddWithValue("$updatedAt", reminder.UpdatedAt.UtcDateTime.ToString("O"));
            insert.ExecuteNonQuery();
        }
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

        EnsureColumn(connection, "SyncId", "TEXT NULL");
        EnsureColumn(connection, "UpdatedAtUtc", "TEXT NULL");
        EnsureColumn(connection, "Deleted", "INTEGER NOT NULL DEFAULT 0");
        using var backfill = connection.CreateCommand();
        backfill.CommandText = @"UPDATE Reminders SET SyncId = lower(hex(randomblob(16))) WHERE SyncId IS NULL OR SyncId = '';
UPDATE Reminders SET UpdatedAtUtc = strftime('%Y-%m-%dT%H:%M:%fZ','now') WHERE UpdatedAtUtc IS NULL OR UpdatedAtUtc = '';";
        backfill.ExecuteNonQuery();
    }

    private static void EnsureColumn(SqliteConnection connection, string column, string definition)
    {
        var found = false;
        using var schema = connection.CreateCommand();
        schema.CommandText = "PRAGMA table_info(Reminders);";
        using var reader = schema.ExecuteReader();
        while (reader.Read()) if (reader.GetString(1) == column) { found = true; break; }
        if (!found) { using var alter = connection.CreateCommand(); alter.CommandText = $"ALTER TABLE Reminders ADD COLUMN {column} {definition}"; alter.ExecuteNonQuery(); }
    }
}
