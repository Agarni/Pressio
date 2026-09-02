using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;
using Pressio.Models;

namespace Pressio.Services;

public static class PdfReportService
{
    private const float PageW = 595f;
    private const float PageH = 842f;
    private const float Margin = 48f;

    public static void Export(string path, Patient patient, IReadOnlyList<BloodPressureMeasurement> measurements, string filterDescription)
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
        var line = new SKPaint { Color = SKColor.Parse("#E0E3F1"), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };

        var canvas = document.BeginPage(PageW, PageH);
        float y = Margin + 10;

        canvas.DrawText("Pressio — Relatório de pressão", Margin, y, titleFont, primary); y += 26;
        canvas.DrawText($"Paciente: {patient.Name}", Margin, y, labelFont, text); y += 18;
        canvas.DrawText($"Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm}   •   {filterDescription}", Margin, y, smallFont, muted); y += 16;
        canvas.DrawLine(Margin, y, PageW - Margin, y, line); y += 24;

        canvas.DrawText("Resumo", Margin, y, sectionFont, text); y += 22;
        var avgSys = (int)Math.Round(measurements.Average(m => m.Systolic), MidpointRounding.AwayFromZero);
        var avgDia = (int)Math.Round(measurements.Average(m => m.Diastolic), MidpointRounding.AwayFromZero);
        canvas.DrawText($"Média dos registros: {BloodPressureMeasurement.Format(avgSys, avgDia)} mmHg ({measurements.Count} registros)", Margin, y, bodyFont, text); y += 19;
        y = DrawKv(canvas, "Antes da medicação", SummarizeByMedication(measurements, MedicationTiming.BeforeMedication), y, labelFont, bodyFont, text, muted);
        y = DrawKv(canvas, "Depois da medicação", SummarizeByMedication(measurements, MedicationTiming.AfterMedication), y, labelFont, bodyFont, text, muted);

        if (measurements.Count > 1)
            y = DrawChart(canvas, measurements, y + 8, smallFont, muted, line);
        else
            y += 4;

        canvas.DrawText("Registros", Margin, y, sectionFont, text); y += 20;
        float width = PageW - Margin * 2;
        var cols = new (string Title, float W)[] {
            ("Data e hora", 96f), ("Pressão", 62f), ("Medicação", 86f), ("Contexto", 122f), ("Observação", width - 96f - 62f - 86f - 122f)
        };

        float cellX = Margin;
        canvas.DrawRect(Margin, y - 14, width, 17, Paint(SKColor.Parse("#EEF0FF")));
        foreach (var c in cols)
        {
            canvas.DrawText(c.Title, cellX + 6, y, labelFont, text);
            cellX += c.W;
        }
        y += 14;
        canvas.DrawLine(Margin, y, PageW - Margin, y, line);
        y += 6;

        foreach (var m in measurements)
        {
            if (y > PageH - Margin - 12)
            {
                document.EndPage();
                canvas = document.BeginPage(PageW, PageH);
                y = Margin + 10;
            }
            cellX = Margin;
            DrawCell(canvas, m.DisplayDate, cols[0].W, ref cellX, y, bodyFont, text);
            DrawCell(canvas, m.DisplayValue, cols[1].W, ref cellX, y, bodyFont, text);
            DrawCell(canvas, DescribeMedication(m.MedicationTiming), cols[2].W, ref cellX, y, smallFont, muted);
            DrawCell(canvas, m.HasContext ? m.DisplayContext : "—", cols[3].W, ref cellX, y, smallFont, muted);
            DrawCell(canvas, string.IsNullOrWhiteSpace(m.Notes) ? "—" : m.Notes, cols[4].W, ref cellX, y, smallFont, muted);
            y += 16;
        }

        document.EndPage();
        document.Close();
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

    private static float DrawChart(SKCanvas canvas, IReadOnlyList<BloodPressureMeasurement> measurements, float y, SKFont smallFont, SKPaint muted, SKPaint line)
    {
        float left = Margin, width = PageW - Margin * 2, chartH = 110;
        float top = y;
        canvas.DrawLine(left, top + chartH, left + width, top + chartH, line);
        canvas.DrawLine(left, top, left + width, top + chartH, line);

        var ordered = measurements.OrderBy(m => m.MeasuredAt).ToList();
        var min = ordered.Min(m => Math.Min(m.Systolic, m.Diastolic));
        var max = Math.Max(min + 1, ordered.Max(m => Math.Max(m.Systolic, m.Diastolic)));
        float X(int i) => ordered.Count == 1 ? left + width / 2 : left + i * width / (ordered.Count - 1);
        float Y(int v) => top + (max - v) * chartH / (max - min);

        var sysPaint = new SKPaint { Color = SKColor.Parse("#5B5BD6"), Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f, IsAntialias = true };
        var diaPaint = new SKPaint { Color = SKColor.Parse("#8C93BE"), Style = SKPaintStyle.Stroke, StrokeWidth = 1.8f, IsAntialias = true };

        using var sysPath = new SKPath();
        using var diaPath = new SKPath();
        for (var i = 0; i < ordered.Count; i++)
        {
            var sys = new SKPoint(X(i), Y(ordered[i].Systolic));
            var dia = new SKPoint(X(i), Y(ordered[i].Diastolic));
            if (i == 0) { sysPath.MoveTo(sys); diaPath.MoveTo(dia); }
            else { sysPath.LineTo(sys); diaPath.LineTo(dia); }
        }
        canvas.DrawPath(sysPath, sysPaint);
        canvas.DrawPath(diaPath, diaPaint);
        canvas.DrawText("Pressão maior (linha cheia) e pressão menor (linha clara)", left, top + chartH + 16, smallFont, muted);
        return top + chartH + 34;
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
