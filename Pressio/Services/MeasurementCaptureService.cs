using System;
using System.Linq;
using System.Threading.Tasks;

namespace Pressio.Services;

public sealed record CaptureReadingResult(string? Value, string? RawText)
{
    public static readonly CaptureReadingResult None = new(null, null);
}

public interface IMeasurementCaptureService
{
    // true quando a plataforma tem câmera + OCR (ex.: iOS com Vision).
    bool IsSupported { get; }

    // Mostra/abre o fluxo de captura (câmera) e lê a pressão. Value = "130/80" (ou null se não parseou);
    // RawText = o que o OCR reconheceu (para diagnóstico).
    Task<CaptureReadingResult> CaptureAndReadAsync();
}

public static class MeasurementCapture
{
    // Definido em cada host (ex.: iOS usa UIImagePickerController + Vision). O padrão não suporta.
    public static IMeasurementCaptureService Service { get; set; } = new EmptyMeasurementCaptureService();

    private sealed class EmptyMeasurementCaptureService : IMeasurementCaptureService
    {
        public bool IsSupported => false;
        public Task<CaptureReadingResult> CaptureAndReadAsync() => Task.FromResult(CaptureReadingResult.None);
    }
}

public static class OcrPressureParser
{
    // Converte o texto reconhecido pelo OCR em um par (sistólica, diastólica) válido, ou null.
    // Trata tanto "138 89" quanto "138/89", e também o formato abreviado "13/8" (multiplica por 10 quando < 30).
    public static (int Systolic, int Diastolic)? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var separators = new[] { ' ', '/', 'x', 'X', ';', ',', '-', '\t' };
        var tokens = text
            .Replace('.', ' ')
            .Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => new string(t.Where(char.IsDigit).ToArray()))
            .Where(t => t.Length > 0)
            .ToList();

        for (var i = 0; i < tokens.Count - 1; i++)
        {
            if (!TryNormalize(tokens[i], out var sys)) continue;
            if (!TryNormalize(tokens[i + 1], out var dia)) continue;
            if (sys >= 50 && sys <= 300 && dia >= 30 && dia <= 200 && sys > dia)
                return (sys, dia);
        }
        return null;
    }

    private static bool TryNormalize(string token, out int value)
    {
        if (int.TryParse(token, out value) && value > 0)
        {
            // Formato abreviado: 13/8 -> 130/80.
            if (value < 30) value *= 10;
            return true;
        }
        return false;
    }
}
