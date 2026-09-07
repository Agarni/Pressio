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

// Usa UIImagePickerController (câmera) + Vision (rótulos SYS/DIA/PULSE com posição) para localizar
// as linhas de dígitos do monitor e um decodificador dedicado de 7 segmentos para ler a pressão.
public sealed class IosMeasurementCaptureService : IMeasurementCaptureService
{
    private readonly record struct LabelRect(string Text, int Left, int Top, int Width, int Height)
    {
        public int CenterY => Top + Height / 2;
        public int Right => Left + Width;
    }

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
        var handler = new VNImageRequestHandler(cgImage, new NSDictionary());
        NSError? error;
        handler.Perform(new VNRequest[] { textRequest }, out error);

        var rawText = string.Join(" ", ExtractLines(textRequest));
        var labels = LabelRects(textRequest, w, h);
        var gray = ExtractGray(cgImage, w, h);
        var pair = RecognizeByLabels(gray, w, h, labels, out var diagnostics);
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

    private static List<LabelRect> LabelRects(VNRecognizeTextRequest request, int w, int h)
    {
        var result = new List<LabelRect>();
        if (request.Results is not { } results) return result;
        foreach (var obs in results)
        {
            if (obs is not VNRecognizedTextObservation textObs) continue;
            if (textObs.TopCandidates(1) is not { Length: > 0 } cands) continue;
            var text = cands[0].String;
            var bb = textObs.BoundingBox;
            if (bb.IsEmpty) continue;
            result.Add(new LabelRect(
                text,
                (int)(bb.Left * w),
                (int)((1 - bb.Top - bb.Height) * h),
                (int)(bb.Width * w),
                (int)(bb.Height * h)));
        }
        return result;
    }

    // Usa a posição dos rótulos SYS/DIA (que o Vision detecta bem) como âncora das linhas de dígitos.
    private static (int Systolic, int Diastolic)? RecognizeByLabels(float[] gray, int w, int h, List<LabelRect> labels, out string diagnostics)
    {
        var sys = labels.FirstOrDefault(l => l.Text.IndexOf("sys", StringComparison.OrdinalIgnoreCase) >= 0);
        var dia = labels.FirstOrDefault(l => l.Text.IndexOf("dia", StringComparison.OrdinalIgnoreCase) >= 0);
        var pulse = labels.FirstOrDefault(l => l.Text.IndexOf("pulse", StringComparison.OrdinalIgnoreCase) >= 0);

        if (sys.Text is null || dia.Text is null)
        {
            diagnostics = $"Rótulos SYS/DIA não encontrados ({labels.Count}).";
            return null;
        }

        // Os dígitos ficam à DIREITA dos rótulos.
        var digitX0 = Math.Max(sys.Right, dia.Right) + (int)(w * 0.03);
        var digitX1 = w - (int)(w * 0.03);

        var g1 = dia.CenterY - sys.CenterY;
        var g2 = pulse.Text is null ? g1 : Math.Max(g1, pulse.CenterY - dia.CenterY);

        // Janelas ao redor do centro de cada rótulo, com refino para a altura real do dígito.
        var sysBand = RefineBand(gray, w, digitX0, digitX1, ClampInt(sys.CenterY - (int)(g1 * 0.6), 0, h), ClampInt(sys.CenterY + (int)(g1 * 0.6), 0, h), sys.CenterY);
        var diaBand = RefineBand(gray, w, digitX0, digitX1, ClampInt(dia.CenterY - (int)(g2 * 0.6), 0, h), ClampInt(dia.CenterY + (int)(g2 * 0.6), 0, h), dia.CenterY);
        if (sysBand.Top < 0 || diaBand.Top < 0)
        {
            diagnostics = $"Refino falhou: SYS@{sys.CenterY} DIA@{dia.CenterY}.";
            return null;
        }

        var sysRow = SevenSegmentDecoder.DecodeRow(gray, w, h, digitX0, sysBand.Top, digitX1, sysBand.Bottom);
        var diaRow = SevenSegmentDecoder.DecodeRow(gray, w, h, digitX0, diaBand.Top, digitX1, diaBand.Bottom);

        diagnostics = $"SYS@[{sys.CenterY}] DIA@[{dia.CenterY}] x[{digitX0}-{digitX1}] bandas SYS[{sysBand.Top}-{sysBand.Bottom}] DIA[{diaBand.Top}-{diaBand.Bottom}] -> SYS=\"{sysRow}\" DIA=\"{diaRow}\"";

        if (!int.TryParse(sysRow, out var s) || !int.TryParse(diaRow, out var d)) return null;
        if (s < 50 || s > 300 || d < 30 || d > 200 || s <= d) return null;
        return (s, d);
    }

    // Encontra a linha (grupo contíguo de pixels "ativos") que contém centerY, dentro da janela dada.
    private static (int Top, int Bottom) RefineBand(float[] gray, int w, int x0, int x1, int y0, int y1, int centerY)
    {
        var median = MedianRegion(gray, w, x0, x1, y0, y1, out var dev);
        if (median < 0) return (-1, -1);

        static bool Active(float v, float median, float dev) => Math.Abs(v - median) > dev;

        // Expandir para fora a partir de centerY.
        var top = -1; var bottom = -1;
        for (var y = centerY; y >= y0; y--)
            if (RowActive(gray, w, x0, x1, y, median, dev)) top = y; else break;
        if (top < 0)
        {
            // centerY não é ativo; tenta achar a linha ativa mais próxima dentro da janela.
            for (var y = y0; y < y1; y++) if (RowActive(gray, w, x0, x1, y, median, dev)) { top = y; break; }
        }
        for (var y = centerY; y < y1; y++)
            if (RowActive(gray, w, x0, x1, y, median, dev)) bottom = y; else break;
        if (top < 0 || bottom < 0 || bottom < top) return (-1, -1);
        return (top, bottom + 1);
    }

    private static bool RowActive(float[] gray, int w, int x0, int x1, int y, float median, float dev)
    {
        for (var x = x0; x < x1; x++)
            if (Math.Abs(gray[y * w + x] - median) > dev) return true;
        return false;
    }

    private static float MedianRegion(float[] gray, int w, int x0, int x1, int y0, int y1, out float dev)
    {
        var vals = new List<int>(x1 - x0);
        var minV = 255; var maxV = 0;
        for (var y = Math.Max(0, y0); y < Math.Min(y1, gray.Length / Math.Max(1, w)); y++)
            for (var x = Math.Max(0, x0); x < Math.Min(x1, w); x++)
            {
                var v = (int)gray[y * w + x];
                vals.Add(v);
                if (v < minV) minV = v;
                if (v > maxV) maxV = v;
            }
        if (vals.Count == 0) { dev = 0; return -1; }
        vals.Sort();
        dev = Math.Max(20, (maxV - minV) * 0.25f);
        return vals[vals.Count / 2];
    }

    private static int ClampInt(int v, int lo, int hi) => Math.Min(hi, Math.Max(lo, v));

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
