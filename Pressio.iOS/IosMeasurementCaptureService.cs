using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoreGraphics;
using Foundation;
using UIKit;
using Vision;
using Pressio.Services;

namespace Pressio.iOS;

// Usa UIImagePickerController (câmera) + Vision (recognição de texto) para ler a pressão do monitor.
public sealed class IosMeasurementCaptureService : IMeasurementCaptureService
{
    public bool IsSupported => true;

    public Task<CaptureReadingResult> CaptureAndReadAsync()
    {
        var root = TopViewController();
        if (root is null) return Task.FromResult(CaptureReadingResult.None);

        var tcs = new TaskCompletionSource<CaptureReadingResult>();
        var picker = new UIImagePickerController();
        if (UIImagePickerController.IsSourceTypeAvailable(UIImagePickerControllerSourceType.Camera))
            picker.SourceType = UIImagePickerControllerSourceType.Camera;
        else
            picker.SourceType = UIImagePickerControllerSourceType.PhotoLibrary;

        picker.FinishedPickingMedia += (_, e) =>
        {
            var result = RunVision(e.OriginalImage);
            picker.DismissViewController(true, null);
            tcs.TrySetResult(result);
        };
        picker.Canceled += (_, _) =>
        {
            picker.DismissViewController(true, null);
            tcs.TrySetResult(CaptureReadingResult.None);
        };

        root.PresentViewController(picker, true, null);
        return tcs.Task;
    }

    private static CaptureReadingResult RunVision(UIImage? image)
    {
        // Normaliza a orientação e reduz a resolução (fotos da câmera são muito grandes e podem
        // vir "de lado" para o Vision, o que impede o reconhecimento).
        var cgImage = NormalizeImage(image);
        if (cgImage is null) return CaptureReadingResult.None;

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
                if (obs is VNRecognizedTextObservation textObs && textObs.TopCandidates(2) is { Length: > 0 } cands)
                {
                    // Junta os candidatos de maior confiança: primeira linha = mais provável.
                    var line = string.Join(" ", cands.Select(c => c.String).Where(s => !string.IsNullOrWhiteSpace(s)));
                    if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
                }
            }
        }

        var text = string.Join(" ", lines);
        var pair = OcrPressureParser.Parse(text);
        var value = pair is { } p ? $"{p.Systolic}/{p.Diastolic}" : null;
        return new CaptureReadingResult(value, text);
    }

    private static CGImage? NormalizeImage(UIImage? image)
    {
        if (image is null || image.CGImage is null) return null;
        var size = image.Size;
        if (size.Width <= 0 || size.Height <= 0) return image.CGImage;

        var maxDim = 1280.0;
        var scale = Math.Min(1.0, maxDim / Math.Max(size.Width, size.Height));
        var w = (nfloat)(size.Width * scale);
        var h = (nfloat)(size.Height * scale);

        UIGraphics.BeginImageContext(new CGSize(w, h));
        image.Draw(new CGRect(0, 0, w, h));
        var normalized = UIGraphics.GetImageFromCurrentImageContext()?.CGImage;
        UIGraphics.EndImageContext();
        return normalized ?? image.CGImage;
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
