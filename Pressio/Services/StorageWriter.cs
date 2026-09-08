using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace Pressio.Services;

// Grava bytes no arquivo escolhido pelo seletor de storage de forma segura por plataforma.
// No desktop/iOS usa OpenWriteAsync; no Android o Avalonia storage não grava stream, então o host
// abastece uma implementação via ContentResolver (SAF).
public interface IStorageWriter
{
    Task<bool> WriteAsync(IStorageFile file, byte[] bytes);
}

public static class StorageWriter
{
    public static IStorageWriter Service { get; set; } = new DefaultStorageWriter();

    private sealed class DefaultStorageWriter : IStorageWriter
    {
        public async Task<bool> WriteAsync(IStorageFile file, byte[] bytes)
        {
            await using var s = await file.OpenWriteAsync();
            s.Write(bytes, 0, bytes.Length);
            return true;
        }
    }
}
