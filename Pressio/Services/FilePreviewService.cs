using System.Threading.Tasks;

namespace Pressio.Services;

public interface IFilePreviewService
{
    Task<bool> PreviewAsync(string path);
}

public static class FilePreview
{
    // Definido em cada host (ex.: iOS usa QLPreviewController). O padrão não faz nada
    // (o desktop/Android resolvem via Avalonia Launcher no MainView).
    public static IFilePreviewService Service { get; set; } = new EmptyFilePreviewService();

    private sealed class EmptyFilePreviewService : IFilePreviewService
    {
        public Task<bool> PreviewAsync(string path) => Task.FromResult(false);
    }
}
