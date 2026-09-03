using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pressio.Models.Sync;

namespace Pressio.Services;

/// <summary>Serializa/desserializa o snapshot de sincronia (`pressio-sync.json`).</summary>
public sealed class SyncStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Serialize(SyncSnapshot snapshot) => JsonSerializer.Serialize(snapshot, Options);

    public SyncSnapshot Deserialize(string json) =>
        JsonSerializer.Deserialize<SyncSnapshot>(json, Options) ?? new SyncSnapshot();

    public void WriteToFile(string path, SyncSnapshot snapshot)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, Serialize(snapshot));
    }

    public SyncSnapshot? ReadFromFile(string path) =>
        File.Exists(path) ? Deserialize(File.ReadAllText(path)) : null;
}
