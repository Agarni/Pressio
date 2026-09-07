using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Foundation;
using UIKit;
using Vision;
using Pressio.Services;

namespace Pressio.iOS;

// Usa UIImagePickerController (câmera) + Vision (recognição de texto) para ler a pressão do monitor.
public sealed class IosMeasurementCaptureService : IMeasurementCaptureService
{
    public bool IsSupported => true;

    public Task<string?> CaptureAndReadAsync()
    {
        var root = TopViewController();
        if (root is null) return Task.FromResult<string?>(null);

        var tcs = new TaskCompletionSource<string?>();
        var picker = new UIImagePickerController();
        if (UIImagePickerController.IsSourceTypeAvailable(UIImagePickerControllerSourceType.Camera))
            picker.SourceType = UIImagePickerControllerSourceType.Camera;
        else
            picker.SourceType = UIImagePickerControllerSourceType.PhotoLibrary;

        picker.FinishedPickingMedia += (_, e) =>
        {
            var image = e.OriginalImage;
            var result = RunVision(image);
            picker.DismissViewController(true, null);
            tcs.TrySetResult(result);
        };
        picker.Canceled += (_, _) =>
        {
            picker.DismissViewController(true, null);
            tcs.TrySetResult(null);
        };

        root.PresentViewController(picker, true, null);
        return tcs.Task;
    }

    private static string? RunVision(UIImage image)
    {
        var cgImage = image.CGImage;
        if (cgImage is null) return null;
        var request = new VNRecognizeTextRequest(null!)
        {
            RecognitionLevel = VNRequestTextRecognitionLevel.Accurate,
            UsesLanguageCorrection = true
        };
        var handler = new VNImageRequestHandler(cgImage, new NSDictionary());
        NSError? error;
        handler.Perform(new VNRequest[] { request }, out error);

        var lines = new List<string>();
        if (request.Results is { } results)
        {
            foreach (var obs in results)
            {
                if (obs is VNRecognizedTextObservation textObs && textObs.TopCandidates(1) is { Length: > 0 } cands)
                    lines.Add(cands[0].String);
            }
        }

        var text = string.Join(" ", lines);
        return OcrPressureParser.Parse(text) is { } pair ? $"{pair.Systolic}/{pair.Diastolic}" : null;
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
