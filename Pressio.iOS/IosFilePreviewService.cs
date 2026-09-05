using System;
using System.Linq;
using System.Threading.Tasks;
using Foundation;
using UIKit;
using Pressio.Services;

namespace Pressio.iOS;

public sealed class IosFilePreviewService : IFilePreviewService
{
    public Task<bool> PreviewAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return Task.FromResult(false);
        var url = NSUrl.FromFilename(path);
        if (url is null) return Task.FromResult(false);
        var root = TopViewController();
        if (root is null) return Task.FromResult(false);
        var controller = UIDocumentInteractionController.FromUrl(url);
        controller.Delegate = new PreviewDelegate(root);
        controller.PresentPreview(true);
        return Task.FromResult(true);
    }

    private static UIViewController? TopViewController()
    {
        var window = UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .SelectMany(s => s.Windows)
            .FirstOrDefault(w => w.IsKeyWindow);
        var root = window?.RootViewController;
        while (root?.PresentedViewController is { } presented) root = presented;
        return root;
    }
}

public sealed class PreviewDelegate : UIDocumentInteractionControllerDelegate
{
    private readonly UIViewController _controller;
    public PreviewDelegate(UIViewController controller) => _controller = controller;

    public override UIViewController ViewControllerForPreview(UIDocumentInteractionController controller) => _controller;
}
