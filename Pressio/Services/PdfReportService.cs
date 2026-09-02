using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Platform;
using SkiaSharp;
using Pressio.Models;

namespace Pressio.Services;

public static class PdfReportService
{
    private const float PageW = 595f;
    private const float PageH = 842f;
    private const float Margin = 48f;

    public static void Export(string path, Patient patient, IReadOnlyList<BloodPressureMeasurement> measurements, string description, bool truncated)
    {
        using var stream = File.Create(path);
        using var document = SKDocument.CreatePdf(stream);

        var titleFont = Font(SKFontStyleWeight.Bold, 20);
        var sectionFont = Font(SKFontStyleWeight.Bold, 13);
        var labelFont = Font(SKFontStyleWeight.SemiBold, 11);
        var bodyFont = Font(SKFontStyleWeight.Normal, 11);
        var smallFont = Font(SKFontStyleWeight.Normal, 9);

        var primary = Paint(SKColor.Parse("#3A3A9C"));
        var text = Paint(SKColor.Parse("#242B4A"));
        var muted = Paint(SKColor.Parse("#73799B"));
        var headerBg = Paint(SKColor.Parse("#EEF0FF"));
        var zebraBg = Paint(SKColor.Parse("#F6F7FC"));
        var line = new SKPaint { Color = SKColor.Parse("#E0E3F1"), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };

        float width = PageW - Margin * 2;
        var cols = new (string Title, float W)[] {
            ("Data e hora", 96f), ("Pressão", 62f), ("Medicação", 86f), ("Contexto", 122f), ("Observação", width - 96f - 62f - 86f - 122f)
        };

        var ordered = measurements.OrderBy(m => m.MeasuredAt).ToList();
        var canvas = document.BeginPage(PageW, PageH);
        float y = Margin + 10;

        // header + icon
        DrawAppIcon(canvas);
        canvas.DrawText("Pressio — Relatório de pressão", Margin + 58, y, titleFont, primary); y += 26;
        canvas.DrawText($"Paciente: {patient.Name}", Margin, y, labelFont, text); y += 18;
        var note = $"Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm}   •   {description}";
        if (truncated) note += "   •   exibindo os últimos 30 registros";
        canvas.DrawText(note, Margin, y, smallFont, muted); y += 16;
        canvas.DrawLine(Margin, y, PageW - Margin, y, line); y += 24;

        // Registros (first)
        canvas.DrawText("Registros", Margin, y, sectionFont, text); y += 20;
        y = DrawTableHeader(canvas, cols, y, width, labelFont, text, headerBg);

        for (var i = 0; i < ordered.Count; i++)
        {
            if (y > PageH - Margin - 12)
            {
                document.EndPage();
                canvas = document.BeginPage(PageW, PageH);
                y = Margin + 12;
                y = DrawTableHeader(canvas, cols, y, width, labelFont, text, headerBg);
            }
            if (i % 2 == 1) canvas.DrawRect(Margin, y - 13, width, 16, zebraBg);
            float cx = Margin;
            var m = ordered[i];
            DrawCell(canvas, m.DisplayDate, cols[0].W, ref cx, y, bodyFont, text);
            DrawCell(canvas, m.DisplayValue, cols[1].W, ref cx, y, bodyFont, text);
            DrawCell(canvas, DescribeMedication(m.MedicationTiming), cols[2].W, ref cx, y, smallFont, muted);
            DrawCell(canvas, m.HasContext ? m.DisplayContext : "—", cols[3].W, ref cx, y, smallFont, muted);
            DrawCell(canvas, string.IsNullOrWhiteSpace(m.Notes) ? "—" : m.Notes, cols[4].W, ref cx, y, smallFont, muted);
            y += 16;
        }

        // Resumo + gráfico (last page)
        if (y > PageH - Margin - 200) { document.EndPage(); canvas = document.BeginPage(PageW, PageH); y = Margin + 12; }
        y += 16;
        canvas.DrawText("Resumo", Margin, y, sectionFont, text); y += 22;
        var avgSys = (int)Math.Round(measurements.Average(m => m.Systolic), MidpointRounding.AwayFromZero);
        var avgDia = (int)Math.Round(measurements.Average(m => m.Diastolic), MidpointRounding.AwayFromZero);
        canvas.DrawText($"Média dos registros: {BloodPressureMeasurement.Format(avgSys, avgDia)} mmHg ({measurements.Count} registros)", Margin, y, bodyFont, text); y += 19;
        y = DrawKv(canvas, "Antes da medicação", SummarizeByMedication(measurements, MedicationTiming.BeforeMedication), y, labelFont, bodyFont, text, muted);
        y = DrawKv(canvas, "Depois da medicação", SummarizeByMedication(measurements, MedicationTiming.AfterMedication), y, labelFont, bodyFont, text, muted);

        if (ordered.Count > 1)
            y = DrawChart(canvas, ordered, y + 10, smallFont, text, muted);

        document.EndPage();
        document.Close();
    }

    private static float DrawTableHeader(SKCanvas canvas, (string Title, float W)[] cols, float y, float width, SKFont labelFont, SKPaint text, SKPaint headerBg)
    {
        float cx = Margin;
        canvas.DrawRect(Margin, y - 14, width, 17, headerBg);
        foreach (var c in cols) { canvas.DrawText(c.Title, cx + 6, y, labelFont, text); cx += c.W; }
        return y + 14;
    }

    private static void DrawAppIcon(SKCanvas canvas)
    {
        try
        {
            using var stream = typeof(PdfReportService).Assembly.GetManifestResourceStream("Pressio.Assets.Icon.png");
            using var bitmap = SKBitmap.Decode(stream);
            if (bitmap != null) canvas.DrawBitmap(bitmap, new SKRect(Margin, Margin - 8, Margin + 46, Margin + 38));
        }
        catch
        {
            // gráfico não é essencial; segue sem ícone se o asset não estiver disponível
        }
    }

    private static string SummarizeByMedication(IReadOnlyList<BloodPressureMeasurement> items, MedicationTiming timing)
    {
        var subset = items.Where(x => x.MedicationTiming == timing).ToList();
        if (subset.Count == 0) return "—";
        var s = (int)Math.Round(subset.Average(x => x.Systolic), MidpointRounding.AwayFromZero);
        var d = (int)Math.Round(subset.Average(x => x.Diastolic), MidpointRounding.AwayFromZero);
        return $"{subset.Count}x  •  média {BloodPressureMeasurement.Format(s, d)}";
    }

    private static string DescribeMedication(MedicationTiming timing) => timing switch
    {
        MedicationTiming.BeforeMedication => "Antes",
        MedicationTiming.AfterMedication => "Depois",
        MedicationTiming.NotApplicable => "Não se aplica",
        _ => "Não informado"
    };

    private static float DrawKv(SKCanvas canvas, string label, string value, float y, SKFont labelFont, SKFont bodyFont, SKPaint text, SKPaint muted)
    {
        canvas.DrawText(label, Margin + 8, y, labelFont, muted);
        canvas.DrawText(value, Margin + 132, y, bodyFont, text);
        return y + 19;
    }

    private static float DrawChart(SKCanvas canvas, IReadOnlyList<BloodPressureMeasurement> ordered, float y, SKFont smallFont, SKPaint text, SKPaint muted)
    {
        float left = Margin, width = PageW - Margin * 2, chartH = 110;
        float top = y;
        canvas.DrawLine(left, top + chartH, left + width, top + chartH, muted);
        var min = ordered.Min(m => Math.Min(m.Systolic, m.Diastolic));
        var max = Math.Max(min + 1, ordered.Max(m => Math.Max(m.Systolic, m.Diastolic)));
        float X(int i) => ordered.Count == 1 ? left + width / 2 : left + i * width / (ordered.Count - 1);
        float Y(int v) => top + (max - v) * chartH / (max - min);

        var sysPoints = ordered.Select((m, i) => new SKPoint(X(i), Y(m.Systolic))).ToList();
        var diaPoints = ordered.Select((m, i) => new SKPoint(X(i), Y(m.Diastolic))).ToList();

        var sysPaint = new SKPaint { Color = SKColor.Parse("#5B5BD6"), Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f, IsAntialias = true };
        var diaPaint = new SKPaint { Color = SKColor.Parse("#8C93BE"), Style = SKPaintStyle.Stroke, StrokeWidth = 1.8f, IsAntialias = true };
        using var sysPath = new SKPath();
        using var diaPath = new SKPath();
        BuildSmoothPath(sysPath, sysPoints);
        BuildSmoothPath(diaPath, diaPoints);
        canvas.DrawPath(sysPath, sysPaint);
        canvas.DrawPath(diaPath, diaPaint);

        var valuePaint = Paint(SKColor.Parse("#242B4A"));
        for (var i = 0; i < ordered.Count; i++)
            canvas.DrawText(ordered[i].DisplayValue, X(i) - 16, Y(ordered[i].Systolic) - 4, smallFont, valuePaint);

        canvas.DrawText("Pressão maior (linha cheia) e pressão menor (linha clara)", left, top + chartH + 16, smallFont, muted);
        return top + chartH + 34;
    }

    private static void BuildSmoothPath(SKPath path, IReadOnlyList<SKPoint> pts)
    {
        if (pts.Count == 0) return;
        path.MoveTo(pts[0]);
        if (pts.Count == 1) return;
        for (var i = 0; i < pts.Count - 1; i++)
        {
            var p0 = pts[Math.Max(0, i - 1)];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            var p3 = pts[Math.Min(pts.Count - 1, i + 2)];
            var c1 = new SKPoint(p1.X + (p2.X - p0.X) / 6f, p1.Y + (p2.Y - p0.Y) / 6f);
            var c2 = new SKPoint(p2.X - (p3.X - p1.X) / 6f, p2.Y - (p3.Y - p1.Y) / 6f);
            path.CubicTo(c1, c2, p2);
        }
    }

    private static void DrawCell(SKCanvas canvas, string value, float width, ref float cellX, float y, SKFont font, SKPaint paint)
    {
        canvas.Save();
        canvas.ClipRect(new SKRect(cellX, y - 13, cellX + width, y + 4));
        canvas.DrawText(value, cellX + 6, y, font, paint);
        canvas.Restore();
        cellX += width;
    }

    private static SKFont Font(SKFontStyleWeight weight, float size)
    {
        var style = new SKFontStyle(weight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        return new SKFont(SKTypeface.FromFamilyName("Helvetica", style), size);
    }

    private static SKPaint Paint(SKColor color) => new() { Color = color, IsAntialias = true };
}
