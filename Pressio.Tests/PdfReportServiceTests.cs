using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pressio.Models;
using Pressio.Services;
using Xunit;

namespace Pressio.Tests;

public class PdfReportServiceTests
{
    private static string Temp() => Path.Combine(Path.GetTempPath(), $"pressio_{Guid.NewGuid():N}.pdf");

    private static List<BloodPressureMeasurement> Readings(int count, DateTime start)
    {
        BloodPressureMeasurement.UseShorthandFormat = false;
        var list = new List<BloodPressureMeasurement>();
        for (var i = 0; i < count; i++)
            list.Add(new BloodPressureMeasurement(
                130 + i % 6, 85 + i % 5,
                start.AddDays(-i).AddHours(i % 8),
                i % 2 == 0 ? MedicationTiming.BeforeMedication : MedicationTiming.AfterMedication,
                i % 3 == 0 ? "café" : null,
                i % 2 == 0 ? MeasurementContext.Caffeine : MeasurementContext.None,
                72 + i % 6));
        return list;
    }

    [Fact]
    public void Export_CreatesValidPdf()
    {
        var patient = new Patient(1, "Maria Silva");
        var readings = Readings(25, DateTime.Now);
        var path = Temp();
        try
        {
            PdfReportService.Export(path, patient, readings, "Últimos 7 dias", truncated: false);
            var bytes = File.ReadAllBytes(path);
            Assert.NotEmpty(bytes);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ExportDoctorLetter_CreatesValidPdf()
    {
        var patient = new Patient(1, "Maria Silva", new DateTime(1960, 5, 10), "Hipertenso");
        var readings = Readings(15, DateTime.Now);
        var path = Temp();
        try
        {
            PdfReportService.ExportDoctorLetter(path, patient, readings, "Todo o histórico");
            var bytes = File.ReadAllBytes(path);
            Assert.NotEmpty(bytes);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
