using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Pressio.Models;

namespace Pressio.Services;

// Geração de PDF 100% gerenciada (PDFsharp), sem SkiaSharp nativo (que crasha no Android).
// Fonte Inter embutida (SIL OFL) via IFontResolver.
public static class PdfReportService
{
    private const float PageW = 595f;
    private const float PageH = 842f;
    private const float Margin = 48f;

    private static readonly XStringFormat BaseLine = new() { Alignment = XStringAlignment.Near, LineAlignment = XLineAlignment.BaseLine };
    private static readonly XStringFormat Center = new() { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.BaseLine };

    private static readonly uint Primary = 0xFF3A3A9C;
    private static readonly uint Text = 0xFF242B4A;
    private static readonly uint Muted = 0xFF73799B;
    private static readonly uint HeaderBg = 0xFFEEF0FF;
    private static readonly uint ZebraBg = 0xFFF6F7FC;
    private static readonly uint Line = 0xFFE0E3F1;
    private static readonly uint Hi = 0xFFB42318;
    private static readonly uint Sys = 0xFF5B5BD6;
    private static readonly uint Dia = 0xFF8C93BE;
    private static readonly uint White = 0xFFFFFFFF;

    static PdfReportService()
    {
        GlobalFontSettings.FontResolver = new InterResolver();
    }

    public static void Export(string path, Patient patient, IReadOnlyList<BloodPressureMeasurement> measurements, string description, bool truncated)
    {
        using var fs = File.Create(path);
        Export(fs, patient, measurements, description, truncated);
    }

    public static void Export(Stream stream, Patient patient, IReadOnlyList<BloodPressureMeasurement> measurements, string description, bool truncated)
    {
        var total = CountPages(patient, measurements, description, truncated, letter: false);
        using var doc = new PdfDocument();
        RenderReport(doc, patient, measurements, description, truncated, total);
        doc.Save(stream);
    }

    public static void ExportDoctorLetter(string path, Patient patient, IReadOnlyList<BloodPressureMeasurement> measurements, string description)
    {
        using var fs = File.Create(path);
        ExportDoctorLetter(fs, patient, measurements, description);
    }

    public static void ExportDoctorLetter(Stream stream, Patient patient, IReadOnlyList<BloodPressureMeasurement> measurements, string description)
    {
        var total = CountPages(patient, measurements, description, truncated: false, letter: true);
        using var doc = new PdfDocument();
        RenderLetter(doc, patient, measurements, description, total);
        doc.Save(stream);
    }

    private static int CountPages(Patient patient, IReadOnlyList<BloodPressureMeasurement> measurements, string description, bool truncated, bool letter)
    {
        using var ms = new MemoryStream();
        using var doc = new PdfDocument();
        var pages = letter
            ? RenderLetter(doc, patient, measurements, description, totalPages: -1)
            : RenderReport(doc, patient, measurements, description, truncated, totalPages: -1);
        doc.Save(ms);
        return pages;
    }

    private static int RenderReport(PdfDocument doc, Patient patient, IReadOnlyList<BloodPressureMeasurement> measurements, string description, bool truncated, int totalPages)
    {
        var titleFont = Font(XFontStyleEx.Bold, 20);
        var sectionFont = Font(XFontStyleEx.Bold, 13);
        var labelFont = Font(XFontStyleEx.Bold, 11);
        var bodyFont = Font(XFontStyleEx.Regular, 11);
        var smallFont = Font(XFontStyleEx.Regular, 9);

        var primary = Brush(Primary);
        var text = Brush(Text);
        var muted = Brush(Muted);
        var headerBg = Brush(HeaderBg);
        var zebraBg = Brush(ZebraBg);
        var line = Pen(Line);
        var dashed = Pen(0xFFC6CCE6);
        dashed.DashStyle = XDashStyle.Dash;
        var hiPaint = Brush(Hi);

        float width = PageW - Margin * 2;
        var cols = new (string Title, float W)[] {
            ("Data e hora", 94f), ("Pressão", 58f), ("FC (bpm)", 44f), ("Medicação", 78f), ("Contexto", 112f), ("Observação", width - 94f - 58f - 44f - 78f - 112f)
        };

        var ordered = measurements.OrderBy(m => m.MeasuredAt).ToList();
        XGraphics? g = null;
        var page = 0;
        void NewPage() { g?.Dispose(); var p = doc.AddPage(); page++; g = XGraphics.FromPdfPage(p); }
        NewPage();
        float y = Margin + 10;

        DrawAppIcon(g);
        DrawText(g, "Pressio — Relatório de pressão", Margin + 58, y, titleFont, primary); y += 26;
        DrawText(g, $"Paciente: {PatientFullName(patient)}", Margin + 58, y, labelFont, text); y += 18;
        var note = $"Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm}   •   {description}";
        if (truncated) note += "   •   exibindo os últimos 30 registros";
        DrawText(g, note, Margin + 58, y, smallFont, muted); y += 16;
        g.DrawLine(line, Margin, y, PageW - Margin, y); y += 24;

        DrawText(g, "Registros", Margin, y, sectionFont, text); y += 20;
        y = DrawTableHeader(g, cols, y, width, labelFont, text, headerBg);

        for (var i = 0; i < ordered.Count; i++)
        {
            if (y > PageH - Margin - 12)
            {
                DrawFooter(g, page, totalPages, patient.Name, smallFont, muted, line);
                NewPage();
                y = Margin + 12;
                y = DrawTableHeader(g, cols, y, width, labelFont, text, headerBg);
            }
            if (i % 2 == 1) g.DrawRectangle(zebraBg, Margin, y - 13, width, 16);
            float cx = Margin;
            var m = ordered[i];
            cx = DrawCell(g, m.DisplayDate, cols[0].W, cx, y, bodyFont, text);
            cx = DrawCell(g, m.DisplayValue, cols[1].W, cx, y, bodyFont, text);
            cx = DrawCell(g, m.HeartRate?.ToString() ?? "—", cols[2].W, cx, y, smallFont, muted);
            cx = DrawCell(g, DescribeMedication(m.MedicationTiming), cols[3].W, cx, y, smallFont, muted);
            cx = DrawCell(g, m.HasContext ? m.DisplayContext : "—", cols[4].W, cx, y, smallFont, muted);
            cx = DrawCell(g, string.IsNullOrWhiteSpace(m.Notes) ? "—" : m.Notes, cols[5].W, cx, y, smallFont, muted);
            y += 16;
        }

        if (y > PageH - Margin - 260)
        {
            DrawFooter(g, page, totalPages, patient.Name, smallFont, muted, line);
            NewPage(); y = Margin + 12;
        }
        y += 16;
        DrawText(g, "Resumo", Margin, y, sectionFont, text); y += 22;
        var avgSys = (int)Math.Round(measurements.Average(m => m.Systolic), MidpointRounding.AwayFromZero);
        var avgDia = (int)Math.Round(measurements.Average(m => m.Diastolic), MidpointRounding.AwayFromZero);
        DrawText(g, $"Média dos registros: {BloodPressureMeasurement.Format(avgSys, avgDia)} mmHg ({measurements.Count} registros)", Margin, y, bodyFont, text); y += 19;
        y = DrawKv(g, "Antes da medicação", SummarizeByMedication(measurements, MedicationTiming.BeforeMedication), y, labelFont, bodyFont, text, muted);
        y = DrawKv(g, "Depois da medicação", SummarizeByMedication(measurements, MedicationTiming.AfterMedication), y, labelFont, bodyFont, text, muted);

        var minSys = ordered.Min(m => m.Systolic); var maxSys = ordered.Max(m => m.Systolic);
        var minDia = ordered.Min(m => m.Diastolic); var maxDia = ordered.Max(m => m.Diastolic);
        y = DrawKv(g, "Máxima", $"{maxSys}/{maxDia} mmHg   •   Mínima: {minSys}/{minDia} mmHg", y, labelFont, bodyFont, text, muted);

        var highCount = ordered.Count(m => m.Systolic >= 140 || m.Diastolic >= 90);
        var highPct = measurements.Count == 0 ? 0 : (int)Math.Round(highCount * 100.0 / measurements.Count);
        y = DrawKv(g, "≥ 140/90 (hipertensão)", $"{highCount} de {measurements.Count}  •  {highPct}%", y, labelFont, bodyFont, highCount > 0 ? hiPaint : text, muted);

        var hr = measurements.Where(m => m.HeartRate is not null).ToList();
        var hrText = hr.Count == 0 ? "—" : $"{(int)Math.Round(hr.Average(m => m.HeartRate!.Value))} bpm (média de {hr.Count})";
        y = DrawKv(g, "Frequência cardíaca", hrText, y, labelFont, bodyFont, text, muted);

        y += 6;
        DrawText(g, "Distribuição por faixa", Margin, y, sectionFont, text); y += 20;
        float bx = Margin;
        foreach (PressureCategory cat in Enum.GetValues<PressureCategory>())
        {
            var count = ordered.Count(m => m.Category == cat);
            if (count == 0) continue;
            var chipW = 104f;
            var catBrush = Brush(ParseColor(BloodPressureClassification.Color(cat)));
            DrawRounded(g, catBrush, bx, y - 12, chipW, 18, 5);
            DrawText(g, $"{BloodPressureClassification.Label(cat)}: {count}", bx + 6, y, labelFont, Brush(White));
            bx += chipW + 6;
            if (bx + chipW > PageW - Margin) { bx = Margin; y += 20; }
        }
        y += 24;

        if (ordered.Count > 1)
            y = DrawChart(g, ordered, y + 6, smallFont, text, muted, dashed);

        DrawFooter(g, page, totalPages, patient.Name, smallFont, muted, line);
        g?.Dispose();
        return page;
    }

    private static int RenderLetter(PdfDocument doc, Patient patient, IReadOnlyList<BloodPressureMeasurement> measurements, string description, int totalPages)
    {
        var titleFont = Font(XFontStyleEx.Bold, 20);
        var sectionFont = Font(XFontStyleEx.Bold, 13);
        var labelFont = Font(XFontStyleEx.Bold, 11);
        var bodyFont = Font(XFontStyleEx.Regular, 11);
        var smallFont = Font(XFontStyleEx.Regular, 9);

        var primary = Brush(Primary);
        var text = Brush(Text);
        var muted = Brush(Muted);
        var headerBg = Brush(HeaderBg);
        var zebraBg = Brush(ZebraBg);
        var line = Pen(Line);

        float width = PageW - Margin * 2;
        var ordered = measurements.OrderBy(m => m.MeasuredAt).ToList();
        XGraphics? g = null;
        var page = 0;
        void NewPage() { g?.Dispose(); var p = doc.AddPage(); page++; g = XGraphics.FromPdfPage(p); }
        NewPage();
        float y = Margin + 10;

        DrawAppIcon(g);
        DrawText(g, "Pressio — Carta ao médico", Margin + 58, y, titleFont, primary); y += 26;
        DrawText(g, $"Paciente: {PatientFullName(patient)}", Margin + 58, y, labelFont, text); y += 18;
        DrawText(g, $"Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm}   •   {description}", Margin + 58, y, smallFont, muted); y += 16;
        g.DrawLine(line, Margin, y, PageW - Margin, y); y += 22;

        DrawText(g, string.IsNullOrWhiteSpace(patient.DoctorName) ? "Prezado(a) Dr(a).," : $"Prezado(a) Dr(a). {patient.DoctorName},", Margin, y, bodyFont, text); y += 20;
        y = WrapText(g, $"Segue o acompanhamento da pressão arterial de {FirstName(patient)}, com {measurements.Count} registro(s) no período. Abaixo está o resumo dos valores e as leituras mais relevantes para sua avaliação.", Margin, y, width, bodyFont, text, 15); y += 12;

        if (!string.IsNullOrWhiteSpace(patient.HealthDetails) || !string.IsNullOrWhiteSpace(patient.Medications) || !string.IsNullOrWhiteSpace(patient.DoctorName))
        {
            DrawText(g, "Dados do usuário", Margin, y, sectionFont, text); y += 18;
            if (!string.IsNullOrWhiteSpace(patient.Medications)) y = DrawKv(g, "Medicações em uso", patient.Medications.Replace(';', ','), y, labelFont, bodyFont, text, muted);
            if (!string.IsNullOrWhiteSpace(patient.HealthDetails)) y = DrawKv(g, "Dados de saúde", patient.HealthDetails, y, labelFont, bodyFont, text, muted);
            y += 4;
        }

        DrawText(g, "1. Resumo clínico", Margin, y, sectionFont, text); y += 20;
        var avgSys = (int)Math.Round(measurements.Average(m => m.Systolic), MidpointRounding.AwayFromZero);
        var avgDia = (int)Math.Round(measurements.Average(m => m.Diastolic), MidpointRounding.AwayFromZero);
        var avgCat = BloodPressureClassification.Classify(avgSys, avgDia);
        y = DrawKv(g, "Média geral", $"{BloodPressureMeasurement.Format(avgSys, avgDia)} mmHg  •  {BloodPressureClassification.Label(avgCat)}", y, labelFont, bodyFont, text, muted);

        var last = ordered[^1];
        y = DrawKv(g, "Última aferição", $"{last.DisplayValue} mmHg  •  {last.DisplayDate}  •  {last.CategoryLabel}", y, labelFont, bodyFont, text, muted);
        y = DrawKv(g, "Antes da medicação", SummarizeByMedication(measurements, MedicationTiming.BeforeMedication), y, labelFont, bodyFont, text, muted);
        y = DrawKv(g, "Depois da medicação", SummarizeByMedication(measurements, MedicationTiming.AfterMedication), y, labelFont, bodyFont, text, muted);
        y = DrawKv(g, "Por horário", TimeOfDaySummary(ordered), y, labelFont, bodyFont, text, muted);

        y += 8;
        DrawText(g, "2. Distribuição por faixa", Margin, y, sectionFont, text); y += 20;
        float bx = Margin;
        foreach (PressureCategory cat in Enum.GetValues<PressureCategory>())
        {
            var count = ordered.Count(m => m.Category == cat);
            if (count == 0) continue;
            var chipW = 104f;
            DrawRounded(g, Brush(ParseColor(BloodPressureClassification.Color(cat))), bx, y - 12, chipW, 18, 5);
            DrawText(g, $"{BloodPressureClassification.Label(cat)}: {count}", bx + 6, y, labelFont, Brush(White));
            bx += chipW + 6;
            if (bx + chipW > PageW - Margin) { bx = Margin; y += 20; }
        }
        y += 22;

        DrawText(g, "3. Faixas de referência (7ª Diretriz Brasileira de Hipertensão)", Margin, y, sectionFont, text); y += 20;
        y = DrawRangesLegend(g, y, width, labelFont, bodyFont, text, muted, headerBg);

        if (y > PageH - Margin - 60)
        {
            DrawFooter(g, page, totalPages, patient.Name, smallFont, muted, line);
            NewPage(); y = Margin + 12;
        }
        else y += 6;
        DrawText(g, "4. Leituras mais relevantes", Margin, y, sectionFont, text); y += 20;

        var relevant = BuildRelevantReadings(ordered);
        var cols = new (string Title, float W)[] {
            ("Data e hora", 100f), ("Pressão", 58f), ("FC", 34f), ("Classificação", 118f), ("Medicação", 72f), ("Observação", width - 100f - 58f - 34f - 118f - 72f)
        };
        y = DrawTableHeader(g, cols, y, width, labelFont, text, headerBg);
        for (var i = 0; i < relevant.Count; i++)
        {
            if (y > PageH - Margin - 14)
            {
                DrawFooter(g, page, totalPages, patient.Name, smallFont, muted, line);
                NewPage(); y = Margin + 12;
                y = DrawTableHeader(g, cols, y, width, labelFont, text, headerBg);
            }
            if (i % 2 == 1) g.DrawRectangle(zebraBg, Margin, y - 13, width, 16);
            float cx = Margin;
            var m = relevant[i];
            cx = DrawCell(g, m.DisplayDate, cols[0].W, cx, y, bodyFont, text);
            cx = DrawCell(g, m.DisplayValue, cols[1].W, cx, y, bodyFont, text);
            cx = DrawCell(g, m.HeartRate?.ToString() ?? "—", cols[2].W, cx, y, smallFont, muted);
            var catBrush = Brush(ParseColor(BloodPressureClassification.Color(m.Category)));
            g.Save();
            g.IntersectClip(new XRect(cx, y - 13, cols[3].W, 17));
            var pillW = Math.Min(Measure(g, m.CategoryLabel, smallFont) + 14f, cols[3].W - 8f);
            DrawRounded(g, catBrush, cx + 4, y - 10, pillW, 14, 4);
            DrawText(g, m.CategoryLabel, cx + 11, y, smallFont, Brush(White));
            g.Restore();
            cx += cols[3].W;
            cx = DrawCell(g, DescribeMedication(m.MedicationTiming), cols[4].W, cx, y, smallFont, muted);
            cx = DrawCell(g, string.IsNullOrWhiteSpace(m.Notes) ? "—" : m.Notes, cols[5].W, cx, y, smallFont, muted);
            y += 16;
        }

        DrawFooter(g, page, totalPages, patient.Name, smallFont, muted, line);
        g?.Dispose();
        return page;
    }

    private static string PatientFullName(Patient p)
        => string.IsNullOrWhiteSpace(p.FullName) ? p.Name : p.FullName;

    private static string FirstName(Patient p)
        => PatientFullName(p).Split(' ')[0];

    private static IReadOnlyList<BloodPressureMeasurement> BuildRelevantReadings(IReadOnlyList<BloodPressureMeasurement> ordered)
    {
        var result = new List<BloodPressureMeasurement>();
        result.AddRange(ordered.TakeLast(8));
        var maxSys = ordered.OrderByDescending(m => m.Systolic).FirstOrDefault();
        var maxDia = ordered.OrderByDescending(m => m.Diastolic).FirstOrDefault();
        foreach (var extra in new[] { maxSys, maxDia })
            if (extra is not null && !result.Contains(extra)) result.Add(extra);
        return result.OrderBy(m => m.MeasuredAt).ToList();
    }

    private static string TimeOfDaySummary(IReadOnlyList<BloodPressureMeasurement> items)
    {
        var parts = new List<string>();
        foreach (var (label, pred) in new (string, Func<BloodPressureMeasurement, bool>)[] {
            ("Madrugada", m => m.MeasuredAt.Hour < 6),
            ("Manhã", m => m.MeasuredAt.Hour >= 6 && m.MeasuredAt.Hour < 12),
            ("Tarde", m => m.MeasuredAt.Hour >= 12 && m.MeasuredAt.Hour < 18),
            ("Noite", m => m.MeasuredAt.Hour >= 18) })
        {
            var sub = items.Where(pred).ToList();
            if (sub.Count == 0) continue;
            var s = (int)Math.Round(sub.Average(x => x.Systolic), MidpointRounding.AwayFromZero);
            var d = (int)Math.Round(sub.Average(x => x.Diastolic), MidpointRounding.AwayFromZero);
            parts.Add($"{label} {BloodPressureMeasurement.Format(s, d)}");
        }
        return parts.Count == 0 ? "—" : string.Join("   |   ", parts);
    }

    private static float DrawRangesLegend(XGraphics g, float y, float width, XFont labelFont, XFont bodyFont, XBrush text, XBrush muted, XBrush headerBg)
    {
        var rows = new (string Range, string Category, string Color)[] {
            ("< 120 e < 80", "Ótima", BloodPressureClassification.Color(PressureCategory.Optimal)),
            ("120–129 e 80–84", "Normal", BloodPressureClassification.Color(PressureCategory.Normal)),
            ("130–139 ou 85–89", "Elevada", BloodPressureClassification.Color(PressureCategory.Elevated)),
            ("140–159 ou 90–99", "Hipertensão 1", BloodPressureClassification.Color(PressureCategory.Stage1)),
            ("160–179 ou 100–109", "Hipertensão 2", BloodPressureClassification.Color(PressureCategory.Stage2)),
            ("≥ 180 ou ≥ 110", "Hipertensão 3", BloodPressureClassification.Color(PressureCategory.Stage3)),
        };
        g.DrawRectangle(headerBg, Margin, y - 14, width, 17);
        DrawText(g, "Pressão (mmHg)", Margin + 6, y, labelFont, text);
        DrawText(g, "Classificação", Margin + 180, y, labelFont, text);
        y += 14;
        foreach (var r in rows)
        {
            DrawText(g, r.Range, Margin + 6, y, bodyFont, muted);
            DrawRounded(g, Brush(ParseColor(r.Color)), Margin + 178, y - 10, 12, 12, 3);
            DrawText(g, r.Category, Margin + 196, y, bodyFont, text);
            y += 17;
        }
        return y;
    }

    private static float WrapText(XGraphics g, string text, float x, float y, float maxWidth, XFont font, XBrush brush, float lineHeight)
    {
        var words = text.Split(' ');
        var lineStr = "";
        foreach (var w in words)
        {
            var test = lineStr.Length == 0 ? w : lineStr + " " + w;
            if (Measure(g, test, font) > maxWidth && lineStr.Length > 0)
            {
                DrawText(g, lineStr, x, y, font, brush);
                y += lineHeight;
                lineStr = w;
            }
            else lineStr = test;
        }
        if (lineStr.Length > 0) { DrawText(g, lineStr, x, y, font, brush); y += lineHeight; }
        return y;
    }

    private static float DrawTableHeader(XGraphics g, (string Title, float W)[] cols, float y, float width, XFont labelFont, XBrush text, XBrush headerBg)
    {
        float cx = Margin;
        g.DrawRectangle(headerBg, Margin, y - 14, width, 17);
        foreach (var c in cols) { DrawText(g, c.Title, cx + 6, y, labelFont, text); cx += c.W; }
        return y + 14;
    }

    private static void DrawFooter(XGraphics g, int page, int totalPages, string patientName, XFont font, XBrush muted, XPen line)
    {
        if (g is null) return;
        var y = PageH - Margin + 14;
        g.DrawLine(line, Margin, PageH - Margin, PageW - Margin, PageH - Margin);
        var left = $"Paciente: {(patientName ?? "").Split(' ')[0]}";
        DrawText(g, left, Margin, y, font, muted);
        var pageText = totalPages > 0 ? $"Página {page} de {totalPages}" : $"Página {page}";
        var cw = Measure(g, pageText, font);
        DrawText(g, pageText, (PageW - cw) / 2, y, font, muted);
        DrawText(g, "Pressio", PageW - Margin - Measure(g, "Pressio", font), y, font, muted);
    }

    private static void DrawAppIcon(XGraphics g)
    {
        try
        {
            using var s = typeof(PdfReportService).Assembly.GetManifestResourceStream("Pressio.Assets.Icon.png");
            if (s is null) return;
            using var img = XImage.FromStream(s);
            g.DrawImage(img, Margin, Margin - 6, 42, 42);
        }
        catch { /* ícone não essencial */ }
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

    private static float DrawKv(XGraphics g, string label, string value, float y, XFont labelFont, XFont bodyFont, XBrush text, XBrush muted)
    {
        DrawText(g, label, Margin + 8, y, labelFont, muted);
        DrawText(g, value, Margin + 132, y, bodyFont, text);
        return y + 19;
    }

    private static float DrawChart(XGraphics g, IReadOnlyList<BloodPressureMeasurement> ordered, float y, XFont smallFont, XBrush text, XBrush muted, XPen dashed)
    {
        float left = Margin, width = PageW - Margin * 2, chartH = 120;
        float axisY = y + 6;
        float bottom = axisY + chartH + 6;
        float valueTop = axisY - 4;

        var min = ordered.Min(m => Math.Min(m.Systolic, m.Diastolic));
        var max = Math.Max(min + 1, ordered.Max(m => Math.Max(m.Systolic, m.Diastolic)));
        float Y(int v) => axisY + (max - v) * chartH / (max - min);
        float X(int i) => ordered.Count == 1 ? left + width / 2 : left + i * width / (ordered.Count - 1);

        DrawText(g, "mmHg", left - 4, axisY - 4, smallFont, muted);
        g.DrawLine(Pen(Muted), left, axisY, left, bottom);
        DrawText(g, min.ToString(), left - 4, bottom, smallFont, muted);
        DrawText(g, max.ToString(), left - 4, valueTop, smallFont, muted);

        var refSysY = Y(140);
        var refDiaY = Y(90);
        if (refSysY > axisY && refSysY < bottom) { g.DrawLine(dashed, left, refSysY, left + width, refSysY); DrawText(g, "140", left + width - 22, refSysY - 3, smallFont, muted); }
        if (refDiaY > axisY && refDiaY < bottom) { g.DrawLine(dashed, left, refDiaY, left + width, refDiaY); DrawText(g, "90", left + width - 18, refDiaY - 3, smallFont, muted); }

        var sysPoints = ordered.Select((m, i) => new XPoint(X(i), Y(m.Systolic))).ToArray();
        var diaPoints = ordered.Select((m, i) => new XPoint(X(i), Y(m.Diastolic))).ToArray();
        g.DrawCurve(new XPen(XColor.FromArgb(unchecked((int)Sys)), 2.5), sysPoints);
        g.DrawCurve(new XPen(XColor.FromArgb(unchecked((int)Dia)), 1.8), diaPoints);

        for (var i = 0; i < ordered.Count; i++)
            DrawText(g, ordered[i].Systolic.ToString(), Math.Max(left, X(i) - 16), Y(ordered[i].Systolic) - 4, smallFont, Brush(Text));

        DrawText(g, "Sistólica (linha cheia) e diastólica (linha clara). Tracejado: referências 140/90 mmHg.", left, bottom + 18, smallFont, muted);
        return bottom + 40;
    }

    private static float DrawCell(XGraphics g, string value, float width, float cellX, float y, XFont font, XBrush brush)
    {
        g.Save();
        g.IntersectClip(new XRect(cellX, y - 13, width, 17));
        DrawText(g, value, cellX + 6, y, font, brush);
        g.Restore();
        return cellX + width;
    }

    private static void DrawRounded(XGraphics g, XBrush brush, float x, float y, float w, float h, float r)
        => g.DrawRoundedRectangle(null, brush, x, y, w, h, r, r);

    private static void DrawText(XGraphics g, string value, float x, float y, XFont font, XBrush brush)
        => g.DrawString(value, font, brush, new XRect(x, y, 0, 0), BaseLine);

    private static float Measure(XGraphics g, string value, XFont font)
        => (float)g.MeasureString(value, font).Width;

    private static XFont Font(XFontStyleEx style, float size) => new("Inter", size, style);

    private static XBrush Brush(uint argb) => new XSolidBrush(XColor.FromArgb(unchecked((int)argb)));

    private static XPen Pen(uint argb, float width = 1) => new(XColor.FromArgb(unchecked((int)argb)), width);

    private static uint ParseColor(string hex)
    {
        var h = hex.TrimStart('#');
        var r = Convert.ToByte(h.Substring(0, 2), 16);
        var gc = Convert.ToByte(h.Substring(2, 2), 16);
        var b = Convert.ToByte(h.Substring(4, 2), 16);
        return (uint)(0xFF000000 | (r << 16) | (gc << 8) | b);
    }

    private sealed class InterResolver : IFontResolver
    {
        private readonly byte[] _font = Load();

        private static byte[] Load()
        {
            using var s = typeof(PdfReportService).Assembly.GetManifestResourceStream("Pressio.Assets.Fonts.Inter.ttf");
            using var ms = new MemoryStream();
            s?.CopyTo(ms);
            return ms.ToArray();
        }

        public byte[] GetFont(string faceName) => _font;

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) => new(familyName);
    }
}
