using System;
using System.IO;
using System.Linq;
using Pressio.Models;
using Pressio.Services;
using Xunit;

namespace Pressio.Tests;

public sealed class RepositoriesTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pressio-test-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            if (File.Exists(_dbPath + suffix)) File.Delete(_dbPath + suffix);
    }

    [Fact]
    public void MeasurementRepository_AddGetUpdateDelete()
    {
        var repo = new MeasurementRepository(_dbPath);
        var patient = repo.GetPatients().Single(); // auto-criado "Meu perfil"

        var id = repo.Add(new BloodPressureMeasurement(154, 102, DateTime.Now, MedicationTiming.BeforeMedication, "Dor de cabeça", MeasurementContext.Stress | MeasurementContext.PoorSleep, 72, true, Arm.Right, BodyPosition.Seated), patient.Id);
        Assert.True(id > 0);

        var all = repo.GetRecent(patient.Id);
        var item = Assert.Single(all);
        Assert.Equal(154, item.Systolic);
        Assert.Equal(102, item.Diastolic);
        Assert.Equal(MedicationTiming.BeforeMedication, item.MedicationTiming);
        Assert.Equal("Dor de cabeça", item.Notes);
        Assert.Equal(MeasurementContext.Stress | MeasurementContext.PoorSleep, item.Context);
        Assert.Equal(72, item.HeartRate);
        Assert.True(item.AtRest);
        Assert.Equal(Arm.Right, item.Arm);
        Assert.Equal(BodyPosition.Seated, item.Position);

        repo.Update(item with { Systolic = 140, Diastolic = 92, Notes = null });
        var updated = repo.GetRecent(patient.Id).Single();
        Assert.Equal(140, updated.Systolic);
        Assert.Equal(92, updated.Diastolic);
        Assert.Null(updated.Notes);

        repo.Delete(updated.Id);
        Assert.Empty(repo.GetRecent(patient.Id));
    }

    [Fact]
    public void MeasurementRepository_MultiplePatients()
    {
        var repo = new MeasurementRepository(_dbPath);
        var first = repo.GetPatients().Single();
        var id2 = repo.AddPatient("João", new DateTime(1990, 5, 10), "observação");
        Assert.True(id2 > 0);
        var john = repo.GetPatients().First(p => p.Name == "João");
        Assert.Equal(new DateTime(1990, 5, 10), john.BirthDate);
        Assert.Equal("observação", john.Notes);
        Assert.Equal(2, repo.GetPatients().Count);
    }

    [Fact]
    public void ReminderRepository_RoundTrip()
    {
        var repo = new ReminderRepository(_dbPath);
        var days = ReminderDays.Monday | ReminderDays.Wednesday | ReminderDays.Friday;
        var id = repo.Add(new Reminder(0, new TimeSpan(8, 30, 0), days, true, "Manhã"));
        Assert.True(id > 0);

        var item = repo.GetAll().Single();
        Assert.Equal(new TimeSpan(8, 30, 0), item.Time);
        Assert.Equal(days, item.Days);
        Assert.True(item.Enabled);
        Assert.Equal("Manhã", item.Note);

        repo.Update(item with { Enabled = false, Note = null });
        var updated = repo.GetAll().Single();
        Assert.False(updated.Enabled);
        Assert.Null(updated.Note);

        repo.Delete(item.Id);
        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void SettingsRepository_RoundTrip()
    {
        var repo = new SettingsRepository(_dbPath);
        Assert.Equal("13/8", repo.GetMeasurementDisplayFormat());
        repo.SaveMeasurementDisplayFormat("130/80");
        repo.SaveAppearance("Escuro", "Azul");
        Assert.Equal("130/80", repo.GetMeasurementDisplayFormat());
        Assert.Equal("Escuro", repo.GetAppearance());
        Assert.Equal("Azul", repo.GetPrimaryColor());
    }
}
