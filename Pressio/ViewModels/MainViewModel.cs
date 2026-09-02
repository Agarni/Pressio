
using System.Reactive;
using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using Avalonia;
using Pressio.Models;
using Pressio.Services;
using ReactiveUI;

namespace Pressio.ViewModels;

public class MainViewModel : ViewModelBase
{
    public MainViewModel(bool isMobileLayout = false)
    {
        IsMobileLayout = isMobileLayout;
        Initialize();
    }

    public bool IsMobileLayout { get; }
    public bool IsDesktopLayout => !IsMobileLayout;
    public bool IsMeasurementDialogVisible => IsMeasurementFormVisible && !IsMobileLayout;
    public bool IsMeasurementMobilePageVisible => IsMeasurementFormVisible && IsMobileLayout;
    public bool IsPatientDialogVisible => IsPatientFormVisible && !IsMobileLayout;
    public bool IsPatientMobilePageVisible => IsPatientFormVisible && IsMobileLayout;
    public bool IsSettingsDialogVisible => IsSettingsVisible && !IsMobileLayout;
    public bool IsSettingsMobilePageVisible => IsSettingsVisible && IsMobileLayout;
    public string PatientName => SelectedPatient?.Name ?? "Selecione um paciente";
    public string Initials => string.Concat(PatientName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => x[0])).ToUpperInvariant()[..Math.Min(2, PatientName.Length)];
    public string LastReading => Measurements.FirstOrDefault()?.DisplayValue ?? "—";
    public string LastReadingDetails => Measurements.FirstOrDefault() is { } measurement ? $"{measurement.DisplayDate}  •  {DescribeMedicationTiming(measurement.MedicationTiming)}" : "Nenhuma medição registrada";
    public string WeeklySummary => Measurements.Count == 0 ? "Registre a primeira medição" : $"{Measurements.Count} medições registradas";
    public string AverageReading => Measurements.Count == 0 ? "—" : $"{Measurements.Average(x => x.Systolic):0}/{Measurements.Average(x => x.Diastolic):0}";
    public string MeasurementCount => Measurements.Count.ToString();
    private Points _chartPoints = new();
    public Points ChartPoints { get => _chartPoints; private set => this.RaiseAndSetIfChanged(ref _chartPoints, value); }

    private bool _isMeasurementFormVisible;
    private string _bloodPressureInput = string.Empty;
    private string _measurementError = string.Empty;
    private MedicationTiming _medicationTiming = MedicationTiming.NotInformed;
    private DateTime? _measurementDate = DateTime.Today;
    private TimeSpan? _measurementTime = DateTime.Now.TimeOfDay;
    private string _notes = string.Empty;
    private readonly MeasurementRepository _measurementRepository = new();
    private Patient? _selectedPatient;
    private bool _isPatientFormVisible;
    private string _newPatientName = string.Empty;
    private string _patientError = string.Empty;
    private string _exportStatus = string.Empty;
    private bool _editingPatient;
    private BloodPressureMeasurement? _selectedMeasurement;
    private bool _editingMeasurement;
    private bool _isSettingsVisible;
    private string _selectedAppearance = "Claro";
    private string _selectedPrimaryColor = "Índigo";

    public bool IsMeasurementFormVisible
    {
        get => _isMeasurementFormVisible;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isMeasurementFormVisible, value);
            this.RaisePropertyChanged(nameof(IsMeasurementDialogVisible));
            this.RaisePropertyChanged(nameof(IsMeasurementMobilePageVisible));
        }
    }

    public string BloodPressureInput
    {
        get => _bloodPressureInput;
        set
        {
            this.RaiseAndSetIfChanged(ref _bloodPressureInput, value);
            MeasurementError = string.Empty;
        }
    }

    public string MeasurementError
    {
        get => _measurementError;
        private set => this.RaiseAndSetIfChanged(ref _measurementError, value);
    }

    public MedicationTiming MedicationTiming
    {
        get => _medicationTiming;
        set => this.RaiseAndSetIfChanged(ref _medicationTiming, value);
    }

    public IReadOnlyList<string> MedicationOptions { get; } = new[]
    {
        "Não informado",
        "Antes da medicação",
        "Depois da medicação",
        "Não se aplica"
    };

    public string SelectedMedicationOption
    {
        get => MedicationTiming switch
        {
            MedicationTiming.BeforeMedication => MedicationOptions[1],
            MedicationTiming.AfterMedication => MedicationOptions[2],
            MedicationTiming.NotApplicable => MedicationOptions[3],
            _ => MedicationOptions[0]
        };
        set => MedicationTiming = value switch
        {
            "Antes da medicação" => MedicationTiming.BeforeMedication,
            "Depois da medicação" => MedicationTiming.AfterMedication,
            "Não se aplica" => MedicationTiming.NotApplicable,
            _ => MedicationTiming.NotInformed
        };
    }

    public DateTime? MeasurementDate { get => _measurementDate; set => this.RaiseAndSetIfChanged(ref _measurementDate, value); }
    public TimeSpan? MeasurementTime { get => _measurementTime; set => this.RaiseAndSetIfChanged(ref _measurementTime, value); }
    public string Notes { get => _notes; set => this.RaiseAndSetIfChanged(ref _notes, value); }
    public ObservableCollection<BloodPressureMeasurement> Measurements { get; } = new();
    public ObservableCollection<Patient> Patients { get; } = new();
    public Patient? SelectedPatient
    {
        get => _selectedPatient;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedPatient, value);
            ReloadMeasurements();
            this.RaisePropertyChanged(nameof(PatientName)); this.RaisePropertyChanged(nameof(Initials));
        }
    }
    public BloodPressureMeasurement? SelectedMeasurement { get => _selectedMeasurement; set => this.RaiseAndSetIfChanged(ref _selectedMeasurement, value); }
    public string ImageImportStatus { get; private set; } = "";
    public bool IsPatientFormVisible
    {
        get => _isPatientFormVisible;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isPatientFormVisible, value);
            this.RaisePropertyChanged(nameof(IsPatientDialogVisible));
            this.RaisePropertyChanged(nameof(IsPatientMobilePageVisible));
        }
    }
    public string NewPatientName { get => _newPatientName; set { this.RaiseAndSetIfChanged(ref _newPatientName, value); PatientError = string.Empty; } }
    public string PatientError { get => _patientError; private set => this.RaiseAndSetIfChanged(ref _patientError, value); }
    public string ExportStatus { get => _exportStatus; private set => this.RaiseAndSetIfChanged(ref _exportStatus, value); }
    public string MeasurementFormTitle => _editingMeasurement ? "Editar medição" : "Nova medição";
    public string PatientFormTitle => _editingPatient ? "Editar paciente" : "Novo paciente";
    public bool IsSettingsVisible
    {
        get => _isSettingsVisible;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isSettingsVisible, value);
            this.RaisePropertyChanged(nameof(IsSettingsDialogVisible));
            this.RaisePropertyChanged(nameof(IsSettingsMobilePageVisible));
        }
    }
    public IReadOnlyList<string> AppearanceOptions { get; } = new[] { "Claro", "Escuro" };
    public IReadOnlyList<string> PrimaryColorOptions { get; } = new[] { "Índigo", "Azul", "Verde", "Roxo", "Coral" };
    public string SelectedAppearance
    {
        get => _selectedAppearance;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedAppearance, value);
            App.ApplyAppearance(value, SelectedPrimaryColor);
        }
    }
    public string SelectedPrimaryColor { get => _selectedPrimaryColor; set => this.RaiseAndSetIfChanged(ref _selectedPrimaryColor, value); }

    public ReactiveCommand<Unit, Unit> ShowMeasurementFormCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> CancelMeasurementCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> SaveMeasurementCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> DeleteMeasurementCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> EditMeasurementCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ImportMeasurementImageCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ShowPatientFormCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> SavePatientCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ExportCsvCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> CancelPatientCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> EditPatientCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> DeletePatientCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ShowSettingsCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> SaveSettingsCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> CloseSettingsCommand { get; private set; } = null!;
    public ReactiveCommand<string, Unit> SelectPrimaryColorCommand { get; private set; } = null!;

    private void Initialize()
    {
        ShowMeasurementFormCommand = ReactiveCommand.Create(() =>
        {
            _editingMeasurement = false;
            BloodPressureInput = string.Empty;
            Notes = string.Empty;
            MeasurementDate = DateTime.Today;
            MeasurementTime = DateTime.Now.TimeOfDay;
            MedicationTiming = MedicationTiming.NotInformed;
            this.RaisePropertyChanged(nameof(SelectedMedicationOption));
            this.RaisePropertyChanged(nameof(MeasurementFormTitle));
            IsMeasurementFormVisible = true;
        });
        CancelMeasurementCommand = ReactiveCommand.Create(() =>
        {
            IsMeasurementFormVisible = false;
            BloodPressureInput = string.Empty;
            MeasurementError = string.Empty;
            this.RaisePropertyChanged(nameof(MeasurementFormTitle));
        });
        SaveMeasurementCommand = ReactiveCommand.Create(SaveMeasurement);
        DeleteMeasurementCommand = ReactiveCommand.Create(DeleteSelectedMeasurement);
        EditMeasurementCommand = ReactiveCommand.Create(EditSelectedMeasurement);
        ImportMeasurementImageCommand = ReactiveCommand.Create(() => { ImageImportStatus = "Importação por imagem será habilitada com OCR na próxima etapa."; });
        ShowPatientFormCommand = ReactiveCommand.Create(() => { _editingPatient = false; NewPatientName = string.Empty; IsPatientFormVisible = true; this.RaisePropertyChanged(nameof(PatientFormTitle)); });
        SavePatientCommand = ReactiveCommand.Create(SavePatient);
        CancelPatientCommand = ReactiveCommand.Create(() => { IsPatientFormVisible = false; NewPatientName = string.Empty; PatientError = string.Empty; });
        EditPatientCommand = ReactiveCommand.Create(EditPatient);
        DeletePatientCommand = ReactiveCommand.Create(DeletePatient);
        ShowSettingsCommand = ReactiveCommand.Create(() => { IsSettingsVisible = true; });
        SaveSettingsCommand = ReactiveCommand.Create(() => { App.ApplyAppearance(SelectedAppearance, SelectedPrimaryColor); IsSettingsVisible = false; });
        CloseSettingsCommand = ReactiveCommand.Create(() => { IsSettingsVisible = false; });
        SelectPrimaryColorCommand = ReactiveCommand.Create<string>(color =>
        {
            SelectedPrimaryColor = color;
            App.ApplyAppearance(SelectedAppearance, color);
        });
        ExportCsvCommand = ReactiveCommand.Create(ExportCsv);
        foreach (var patient in _measurementRepository.GetPatients()) Patients.Add(patient);
        SelectedPatient = Patients.FirstOrDefault();
    }

    private void SaveMeasurement()
    {
        if (!BloodPressureParser.TryParse(BloodPressureInput, out var parsed, out var error))
        {
            MeasurementError = error ?? "Não foi possível interpretar a pressão.";
            return;
        }

        // Persistência será adicionada na próxima etapa; por enquanto o fluxo já valida
        // e confirma a medição para preparar a integração com o histórico.
        var measuredAt = (MeasurementDate ?? DateTime.Today).Date.Add(MeasurementTime ?? DateTime.Now.TimeOfDay);
        var measurement = new BloodPressureMeasurement(parsed!.Systolic, parsed.Diastolic, measuredAt, MedicationTiming, string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim());
        if (_editingMeasurement && SelectedMeasurement is { Id: > 0 } existing)
        {
            measurement = measurement with { Id = existing.Id };
            _measurementRepository.Update(measurement);
            var index = Measurements.IndexOf(existing);
            Measurements[index] = measurement;
        }
        else
        {
            if (SelectedPatient is null) { MeasurementError = "Cadastre ou selecione um paciente antes de salvar."; return; }
            var id = _measurementRepository.Add(measurement, SelectedPatient.Id);
            measurement = measurement with { Id = id };
            Measurements.Insert(0, measurement);
        }
        IsMeasurementFormVisible = false;
        BloodPressureInput = measurement.Systolic / 10d + "/" + measurement.Diastolic / 10d;
        MeasurementError = string.Empty;
        Notes = string.Empty;
        MeasurementDate = DateTime.Today;
        MeasurementTime = DateTime.Now.TimeOfDay;
        SelectedMeasurement = null;
        _editingMeasurement = false;
        this.RaisePropertyChanged(nameof(MeasurementFormTitle));
        RefreshDashboard();
    }

    private void DeleteSelectedMeasurement()
    {
        if (SelectedMeasurement is null || SelectedMeasurement.Id == 0) return;
        _measurementRepository.Delete(SelectedMeasurement.Id);
        Measurements.Remove(SelectedMeasurement);
        SelectedMeasurement = null;
        RefreshDashboard();
    }

    private void SavePatient()
    {
        if (string.IsNullOrWhiteSpace(NewPatientName)) { PatientError = "Informe o nome do paciente."; return; }
        var name = NewPatientName.Trim();
        if (_editingPatient && SelectedPatient is { } selected)
        {
            var updated = selected with { Name = name };
            _measurementRepository.UpdatePatient(updated);
            Patients[Patients.IndexOf(selected)] = updated;
            SelectedPatient = updated;
        }
        else
        {
            var id = _measurementRepository.AddPatient(name, null, null);
            var patient = new Patient(id, name);
            Patients.Add(patient);
            SelectedPatient = patient;
        }
        IsPatientFormVisible = false;
    }

    private void ExportCsv()
    {
        if (SelectedPatient is null || Measurements.Count == 0) { ExportStatus = "Não há medições para exportar."; return; }
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Pressio");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"pressio-{SelectedPatient.Name.Replace(' ', '-')}-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        var rows = new[] { "Pressão;Data e hora;Medicação;Observação" }.Concat(Measurements.Select(m => $"{m.DisplayValue};{m.DisplayDate};{DescribeMedicationTiming(m.MedicationTiming)};{m.Notes?.Replace(';', ',') ?? string.Empty}"));
        File.WriteAllLines(path, rows);
        ExportStatus = $"Relatório CSV salvo em: {path}";
    }

    private void EditPatient()
    {
        if (SelectedPatient is null) return;
        _editingPatient = true;
        NewPatientName = SelectedPatient.Name;
        IsPatientFormVisible = true;
        this.RaisePropertyChanged(nameof(PatientFormTitle));
    }

    private void DeletePatient()
    {
        if (SelectedPatient is null || Patients.Count <= 1) { PatientError = "Mantenha ao menos um paciente cadastrado."; return; }
        var patient = SelectedPatient;
        _measurementRepository.DeletePatient(patient.Id);
        Patients.Remove(patient);
        SelectedPatient = Patients.FirstOrDefault();
    }

    private void EditSelectedMeasurement()
    {
        if (SelectedMeasurement is not { } measurement) return;
        _editingMeasurement = true;
        BloodPressureInput = $"{measurement.Systolic}/{measurement.Diastolic}";
        MeasurementDate = measurement.MeasuredAt.Date;
        MeasurementTime = measurement.MeasuredAt.TimeOfDay;
        MedicationTiming = measurement.MedicationTiming;
        Notes = measurement.Notes ?? string.Empty;
        IsMeasurementFormVisible = true;
        this.RaisePropertyChanged(nameof(MeasurementFormTitle));
    }

    private void ReloadMeasurements()
    {
        Measurements.Clear();
        if (SelectedPatient is not null)
            foreach (var measurement in _measurementRepository.GetRecent(SelectedPatient.Id)) Measurements.Add(measurement);
        RefreshDashboard();
    }

    private void RefreshDashboard()
    {
        this.RaisePropertyChanged(nameof(LastReading));
        this.RaisePropertyChanged(nameof(LastReadingDetails));
        this.RaisePropertyChanged(nameof(WeeklySummary));
        this.RaisePropertyChanged(nameof(AverageReading));
        this.RaisePropertyChanged(nameof(MeasurementCount));
        var ordered = Measurements.OrderBy(x => x.MeasuredAt).ToList();
        if (ordered.Count == 0) { ChartPoints = new Points(); return; }
        var min = ordered.Min(x => x.Systolic);
        var max = Math.Max(min + 1, ordered.Max(x => x.Systolic));
        ChartPoints = new Points(ordered.Select((x, i) => new Point(ordered.Count == 1 ? 250 : i * 500d / (ordered.Count - 1), 138 - ((x.Systolic - min) * 108d / (max - min)))));
    }

    private static string DescribeMedicationTiming(MedicationTiming timing) => timing switch
    {
        MedicationTiming.BeforeMedication => "antes da medicação",
        MedicationTiming.AfterMedication => "depois da medicação",
        MedicationTiming.NotApplicable => "não se aplica",
        _ => "medicação não informada"
    };
}
