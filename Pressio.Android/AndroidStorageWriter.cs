using System.IO;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Net;
using Avalonia.Platform.Storage;
using Pressio.Services;

namespace Pressio.Android;

// Grava bytes no arquivo do seletor via ContentResolver (SAF), que o Avalonia storage do Android
// não expõe por stream (OpenWriteAsync lança NotSupportedException).
public sealed class AndroidStorageWriter : IStorageWriter
{
    public Task<bool> WriteAsync(IStorageFile file, byte[] bytes)
    {
        var uriString = file.Path?.ToString();
        if (string.IsNullOrWhiteSpace(uriString)) return Task.FromResult(false);
        var uri = Uri.Parse(uriString);
        var resolver = Application.Context.ContentResolver;
        using var stream = resolver.OpenOutputStream(uri);
        if (stream is null) return Task.FromResult(false);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
        return Task.FromResult(true);
    }
}
