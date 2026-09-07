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

// Usa UIImagePickerController (câmera) + Vision (detecção de retângulo) para localizar o painel
// do monitor e um decodificador dedicado de dígitos de 7 segmentos para ler a pressão.
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
        var cgImage = NormalizeImage(image);
        if (cgImage is null) return CaptureReadingResult.None;

        var w = (int)cgImage.Width;
        var h = (int)cgImage.Height;

        var textRequest = new VNRecognizeTextRequest(null!)
        {
            RecognitionLevel = VNRequestTextRecognitionLevel.Accurate,
            UsesLanguageCorrection = true
        };
        var rectRequest = new VNDetectRectanglesRequest(null!)
        {
            MaximumObservations = 6,
            MinimumSize = 0.15f,
            MinimumAspectRatio = 0.2f
        };
        var handler = new VNImageRequestHandler(cgImage, new NSDictionary());
        NSError? error;
        handler.Perform(new VNRequest[] { textRequest, rectRequest }, out error);

        var rawText = string.Join(" ", ExtractLines(textRequest));
        var pair = RecognizeSevenSegment(cgImage, w, h, rectRequest, out var diagnostics);
        var value = pair is { } p ? $"{p.Systolic}/{p.Diastolic}" : null;
        return new CaptureReadingResult(value, rawText, diagnostics);
    }

    private static IEnumerable<string> ExtractLines(VNRecognizeTextRequest request)
    {
        if (request.Results is not { } results) yield break;
        foreach (var obs in results)
        {
            if (obs is VNRecognizedTextObservation textObs && textObs.TopCandidates(1) is { Length: > 0 } cands)
                yield return cands[0].String;
        }
    }

    // Procura o painel (maior retângulo), fatia as duas maiores linhas de dígitos e lê com o decodificador.
    private static (int Systolic, int Diastolic)? RecognizeSevenSegment(CGImage cgImage, int w, int h, VNDetectRectanglesRequest rectRequest, out string diagnostics)
    {
        var gray = ExtractGray(cgImage, w, h);
        if (gray is null) { diagnostics = "Sem pixels."; return null; }

        var panel = LargestRectangle(rectRequest, w, h);
        if (panel is not { } p) { diagnostics = "Painel não detectado."; return null; }

        var insetX = (int)(p.Width * 0.06);
        var insetY = (int)(p.Height * 0.08);
        var px0 = Math.Max(0, p.Left + insetX);
        var py0 = Math.Max(0, p.Top + insetY);
        var px1 = Math.Min(w, p.Left + p.Width - insetX);
        var py1 = Math.Min(h, p.Top + p.Height - insetY);
        if (px1 - px0 < 20 || py1 - py0 < 20) { diagnostics = "Painel pequeno."; return null; }

        var bands = RowBands(gray, w, px0, px1, py0, py1);
        var big = bands.OrderByDescending(b => b.Height).Take(2).OrderBy(b => b.Top).ToList();
        if (big.Count < 2) { diagnostics = $"Linhas: {bands.Count}."; return null; }

        var sysRow = SevenSegmentDecoder.DecodeRow(gray, w, h, px0, big[0].Top, px1, big[0].Top + big[0].Height);
        var diaRow = SevenSegmentDecoder.DecodeRow(gray, w, h, px0, big[1].Top, px1, big[1].Top + big[1].Height);

        diagnostics = $"Painel {w}x{h} -> bandas [{string.Join(", ", bands.Select(b => b.Height))}] SYS=\"{sysRow}\" DIA=\"{diaRow}\"";

        if (!int.TryParse(sysRow, out var sys) || !int.TryParse(diaRow, out var dia)) return null;
        if (sys < 50 || sys > 300 || dia < 30 || dia > 200 || sys <= dia) return null;
        return (sys, dia);
    }

    private static (int Left, int Top, int Width, int Height)? LargestRectangle(VNDetectRectanglesRequest req, int w, int h)
    {
        if (req.Results is not { } results) return null;
        VNRectangleObservation? best = null;
        var bestArea = 0.0;
        foreach (var obs in results)
        {
            if (obs is not VNRectangleObservation r) continue;
            var area = r.BoundingBox.Width * r.BoundingBox.Height;
            if (area > bestArea) { bestArea = area; best = r; }
        }
        if (best is null) return null;
        var bb = best.BoundingBox;
        return ((int)(bb.Left * w), (int)((1 - bb.Top - bb.Height) * h), (int)(bb.Width * w), (int)(bb.Height * h));
    }

    private static List<(int Top, int Height)> RowBands(float[] gray, int w, int x0, int x1, int y0, int y1)
    {
        var bands = new List<(int Top, int Height)>();
        var counts = new int[y1 - y0];

        // Mediana da região (aproximação do fundo), independente de polaridade.
        var vals = new List<int>(x1 - x0);
        var minVal = 255; var maxVal = 0;
        foreach (var y in Enumerable.Range(y0, y1 - y0))
            foreach (var x in Enumerable.Range(x0, x1 - x0))
            {
                var v = (int)gray[y * w + x];
                vals.Add(v);
                if (v < minVal) minVal = v;
                if (v > maxVal) maxVal = v;
            }
        vals.Sort();
        var median = vals[vals.Count / 2];
        var dev = Math.Max(20, (maxVal - minVal) * 0.25f);

        for (var y = y0; y < y1; y++)
        {
            var c = 0;
            for (var x = x0; x < x1; x++)
                if (Math.Abs(gray[y * w + x] - median) > dev) c++;
            counts[y - y0] = c;
        }

        var start = -1;
        for (var i = 0; i < counts.Length; i++)
        {
            if (counts[i] > 0) { if (start < 0) start = i; }
            else if (start >= 0) { if (i - start >= 5) bands.Add((y0 + start, i - start)); start = -1; }
        }
        if (start >= 0 && counts.Length - start >= 5) bands.Add((y0 + start, counts.Length - start));
        return bands;
    }

    private static float[]? ExtractGray(CGImage cgImage, int w, int h)
    {
        var bytesPerRow = w * 4;
        var bytes = new byte[w * h * 4];
        var colorSpace = CGColorSpace.CreateDeviceRGB();
        var ctx = new CGBitmapContext(bytes, w, h, 8, bytesPerRow, colorSpace, CGImageAlphaInfo.PremultipliedLast);
        ctx.DrawImage(new CGRect(0, 0, w, h), cgImage);
        ctx.Flush();
        var gray = new float[w * h];
        for (var i = 0; i < w * h; i++)
        {
            var r = bytes[i * 4]; var g = bytes[i * 4 + 1]; var b = bytes[i * 4 + 2];
            gray[i] = (r + g + b) / 3f;
        }
        return gray;
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
