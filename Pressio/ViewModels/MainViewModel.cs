
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
    private Points _diastolicChartPoints = new();
    public Points DiastolicChartPoints { get => _diastolicChartPoints; private set => this.RaiseAndSetIfChanged(ref _diastolicChartPoints, value); }
    public string BeforeMedicationSummary { get; private set; } = "—";
    public string AfterMedicationSummary { get; private set; } = "—";
    public IReadOnlyList<TimeSlotInfo> TimeDistribution { get; private set; } = Array.Empty<TimeSlotInfo>();
    public IReadOnlyList<ContextCountInfo> ContextCounts { get; private set; } = Array.Empty<ContextCountInfo>();

    private bool _isMeasurementFormVisible;
    private string _bloodPressureInput = string.Empty;
    private string _measurementError = string.Empty;
    private MedicationTiming _medicationTiming = MedicationTiming.NotInformed;
    private DateTime? _measurementDate = DateTime.Today;
    private TimeSpan? _measurementTime = DateTime.Now.TimeOfDay;
    private string _notes = string.Empty;
    private readonly MeasurementRepository _measurementRepository = new();
    private readonly SettingsRepository _settingsRepository = new();
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
    private bool _isConfirmDialogVisible;
    private string _confirmMessage = string.Empty;
    private ConfirmationAction _pendingConfirmation;
    private List<BloodPressureMeasurement> _sourceMeasurements = new();
    private string _filterPeriod = "Todo o histórico";
    private string _filterMedication = "Todas";
    private string _filterTimeOfDay = "Todos os horários";
    private string _filterSearch = string.Empty;

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
    public ObservableCollection<ContextOption> ContextOptions { get; } = new();
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
        set => this.RaiseAndSetIfChanged(ref _selectedAppearance, value);
    }
    public string SelectedPrimaryColor { get => _selectedPrimaryColor; set => this.RaiseAndSetIfChanged(ref _selectedPrimaryColor, value); }
    public bool IsConfirmDialogVisible { get => _isConfirmDialogVisible; private set => this.RaiseAndSetIfChanged(ref _isConfirmDialogVisible, value); }
    public string ConfirmMessage { get => _confirmMessage; private set => this.RaiseAndSetIfChanged(ref _confirmMessage, value); }
    public IReadOnlyList<string> FilterPeriodOptions { get; } = new[] { "Todo o histórico", "Hoje", "Últimos 7 dias", "Últimos 30 dias" };
    public IReadOnlyList<string> FilterMedicationOptions { get; } = new[] { "Todas", "Antes da medicação", "Depois da medicação", "Não informado", "Não se aplica" };
    public IReadOnlyList<string> FilterTimeOfDayOptions { get; } = new[] { "Todos os horários", "Madrugada", "Manhã", "Tarde", "Noite" };
    public string FilterPeriod
    {
        get => _filterPeriod;
        set { if (_filterPeriod != value) { _filterPeriod = value; this.RaisePropertyChanged(nameof(FilterPeriod)); ApplyFilters(); } }
    }
    public string FilterMedication
    {
        get => _filterMedication;
        set { if (_filterMedication != value) { _filterMedication = value; this.RaisePropertyChanged(nameof(FilterMedication)); ApplyFilters(); } }
    }
    public string FilterTimeOfDay
    {
        get => _filterTimeOfDay;
        set { if (_filterTimeOfDay != value) { _filterTimeOfDay = value; this.RaisePropertyChanged(nameof(FilterTimeOfDay)); ApplyFilters(); } }
    }
    public string FilterSearch
    {
        get => _filterSearch;
        set { if (_filterSearch != value) { _filterSearch = value; this.RaisePropertyChanged(nameof(FilterSearch)); ApplyFilters(); } }
    }

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
    public ReactiveCommand<Unit, Unit> ConfirmDeleteCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> CancelDeleteCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ClearFiltersCommand { get; private set; } = null!;

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
            SetContext(MeasurementContext.None);
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
        SaveSettingsCommand = ReactiveCommand.Create(() => { App.ApplyAppearance(SelectedAppearance, SelectedPrimaryColor); _settingsRepository.SaveAppearance(SelectedAppearance, SelectedPrimaryColor); IsSettingsVisible = false; });
        CloseSettingsCommand = ReactiveCommand.Create(() => { IsSettingsVisible = false; });
        SelectPrimaryColorCommand = ReactiveCommand.Create<string>(color => SelectedPrimaryColor = color);
        CancelDeleteCommand = ReactiveCommand.Create(() => { IsConfirmDialogVisible = false; });
        ConfirmDeleteCommand = ReactiveCommand.Create(ExecuteConfirmedDelete);
        ClearFiltersCommand = ReactiveCommand.Create(() =>
        {
            FilterPeriod = "Todo o histórico";
            FilterMedication = "Todas";
            FilterTimeOfDay = "Todos os horários";
            FilterSearch = string.Empty;
        });
        ExportCsvCommand = ReactiveCommand.Create(ExportCsv);
        foreach (var option in MeasurementContextInfo.AllContexts) ContextOptions.Add(new ContextOption(option.Value, option.Label));
        LoadAppSettings();
        foreach (var patient in _measurementRepository.GetPatients()) Patients.Add(patient);
        SelectedPatient = Patients.FirstOrDefault();
    }

    private void LoadAppSettings()
    {
        _selectedAppearance = _settingsRepository.GetAppearance();
        _selectedPrimaryColor = _settingsRepository.GetPrimaryColor();
        this.RaisePropertyChanged(nameof(SelectedAppearance));
        this.RaisePropertyChanged(nameof(SelectedPrimaryColor));
        App.ApplyAppearance(_selectedAppearance, _selectedPrimaryColor);
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
        var context = SelectedContext();
        var measurement = new BloodPressureMeasurement(parsed!.Systolic, parsed.Diastolic, measuredAt, MedicationTiming, string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(), context);
        if (_editingMeasurement && SelectedMeasurement is { Id: > 0 } existing)
        {
            measurement = measurement with { Id = existing.Id };
            _measurementRepository.Update(measurement);
        }
        else
        {
            if (SelectedPatient is null) { MeasurementError = "Cadastre ou selecione um paciente antes de salvar."; return; }
            var id = _measurementRepository.Add(measurement, SelectedPatient.Id);
            measurement = measurement with { Id = id };
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
        ReloadMeasurements();
    }

    private void DeleteSelectedMeasurement()
    {
        if (SelectedMeasurement is not { Id: > 0 } measurement) return;
        _pendingConfirmation = ConfirmationAction.DeleteMeasurement;
        ConfirmMessage = $"Excluir a medição de {measurement.DisplayValue} em {measurement.DisplayDate}?";
        IsConfirmDialogVisible = true;
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
        PatientError = string.Empty;
        _pendingConfirmation = ConfirmationAction.DeletePatient;
        ConfirmMessage = $"Excluir o paciente \"{SelectedPatient.Name}\" e todas as suas medições?";
        IsConfirmDialogVisible = true;
    }

    private void ExecuteConfirmedDelete()
    {
        IsConfirmDialogVisible = false;
        switch (_pendingConfirmation)
        {
            case ConfirmationAction.DeleteMeasurement:
                if (SelectedMeasurement is { Id: > 0 } measurement)
                {
                    _measurementRepository.Delete(measurement.Id);
                    SelectedMeasurement = null;
                    ReloadMeasurements();
                }
                break;
            case ConfirmationAction.DeletePatient:
                if (SelectedPatient is { } patient)
                {
                    _measurementRepository.DeletePatient(patient.Id);
                    Patients.Remove(patient);
                    SelectedPatient = Patients.FirstOrDefault();
                }
                break;
        }
        _pendingConfirmation = ConfirmationAction.None;
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
        SetContext(measurement.Context);
        IsMeasurementFormVisible = true;
        this.RaisePropertyChanged(nameof(MeasurementFormTitle));
    }

    private void ReloadMeasurements()
    {
        _sourceMeasurements = SelectedPatient is not null ? _measurementRepository.GetRecent(SelectedPatient.Id).ToList() : new();
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        IEnumerable<BloodPressureMeasurement> query = _sourceMeasurements;

        (DateTime Start, DateTime End)? range = FilterPeriod switch
        {
            "Hoje" => (DateTime.Today, DateTime.Today),
            "Últimos 7 dias" => (DateTime.Today.AddDays(-6), DateTime.Today),
            "Últimos 30 dias" => (DateTime.Today.AddDays(-29), DateTime.Today),
            _ => null
        };
        if (range is { } active) query = query.Where(m => m.MeasuredAt.Date >= active.Start && m.MeasuredAt.Date <= active.End);

        query = FilterMedication switch
        {
            "Antes da medicação" => query.Where(m => m.MedicationTiming == MedicationTiming.BeforeMedication),
            "Depois da medicação" => query.Where(m => m.MedicationTiming == MedicationTiming.AfterMedication),
            "Não informado" => query.Where(m => m.MedicationTiming == MedicationTiming.NotInformed),
            "Não se aplica" => query.Where(m => m.MedicationTiming == MedicationTiming.NotApplicable),
            _ => query
        };

        query = FilterTimeOfDay switch
        {
            "Madrugada" => query.Where(m => m.MeasuredAt.Hour < 6),
            "Manhã" => query.Where(m => m.MeasuredAt.Hour >= 6 && m.MeasuredAt.Hour < 12),
            "Tarde" => query.Where(m => m.MeasuredAt.Hour >= 12 && m.MeasuredAt.Hour < 18),
            "Noite" => query.Where(m => m.MeasuredAt.Hour >= 18),
            _ => query
        };

        var search = FilterSearch?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(m => m.Notes?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);

        Measurements.Clear();
        foreach (var measurement in query) Measurements.Add(measurement);
        RefreshDashboard();
    }

    private void RefreshDashboard()
    {
        this.RaisePropertyChanged(nameof(LastReading));
        this.RaisePropertyChanged(nameof(LastReadingDetails));
        this.RaisePropertyChanged(nameof(WeeklySummary));
        this.RaisePropertyChanged(nameof(AverageReading));
        this.RaisePropertyChanged(nameof(MeasurementCount));
        this.RaisePropertyChanged(nameof(BeforeMedicationSummary));
        this.RaisePropertyChanged(nameof(AfterMedicationSummary));
        this.RaisePropertyChanged(nameof(TimeDistribution));
        this.RaisePropertyChanged(nameof(ContextCounts));

        var ordered = Measurements.OrderBy(x => x.MeasuredAt).ToList();
        BeforeMedicationSummary = SummarizeByMedication(ordered, MedicationTiming.BeforeMedication);
        AfterMedicationSummary = SummarizeByMedication(ordered, MedicationTiming.AfterMedication);
        TimeDistribution = BuildTimeDistribution(ordered);
        ContextCounts = BuildContextCounts(ordered);
        if (ordered.Count == 0) { ChartPoints = new Points(); DiastolicChartPoints = new Points(); return; }
        var min = ordered.Min(x => Math.Min(x.Systolic, x.Diastolic));
        var max = Math.Max(min + 1, ordered.Max(x => Math.Max(x.Systolic, x.Diastolic)));
        ChartPoints = new Points(ordered.Select((x, i) => new Point(ordered.Count == 1 ? 250 : i * 500d / (ordered.Count - 1), 138 - ((x.Systolic - min) * 108d / (max - min)))));
        DiastolicChartPoints = new Points(ordered.Select((x, i) => new Point(ordered.Count == 1 ? 250 : i * 500d / (ordered.Count - 1), 138 - ((x.Diastolic - min) * 108d / (max - min)))));
    }

    private static string SummarizeByMedication(IReadOnlyList<BloodPressureMeasurement> items, MedicationTiming timing)
    {
        var subset = items.Where(x => x.MedicationTiming == timing).ToList();
        if (subset.Count == 0) return "—";
        return $"{subset.Count}x  ·  média {subset.Average(x => x.Systolic):0}/{subset.Average(x => x.Diastolic):0}";
    }

    private static IReadOnlyList<TimeSlotInfo> BuildTimeDistribution(IReadOnlyList<BloodPressureMeasurement> items) => new[]
    {
        new TimeSlotInfo("Madrugada", items.Count(x => x.MeasuredAt.Hour < 6)),
        new TimeSlotInfo("Manhã", items.Count(x => x.MeasuredAt.Hour >= 6 && x.MeasuredAt.Hour < 12)),
        new TimeSlotInfo("Tarde", items.Count(x => x.MeasuredAt.Hour >= 12 && x.MeasuredAt.Hour < 18)),
        new TimeSlotInfo("Noite", items.Count(x => x.MeasuredAt.Hour >= 18)),
    };

    private static IReadOnlyList<ContextCountInfo> BuildContextCounts(IReadOnlyList<BloodPressureMeasurement> items)
    {
        var result = new List<ContextCountInfo>();
        foreach (var (value, label) in MeasurementContextInfo.AllContexts)
        {
            var count = items.Count(x => (x.Context & value) != 0);
            if (count > 0) result.Add(new ContextCountInfo(label, count));
        }
        return result;
    }

    private MeasurementContext SelectedContext()
    {
        var result = MeasurementContext.None;
        foreach (var option in ContextOptions)
            if (option.IsSelected) result |= option.Context;
        return result;
    }

    private void SetContext(MeasurementContext context)
    {
        foreach (var option in ContextOptions)
            option.IsSelected = (context & option.Context) != 0;
    }

    private static string DescribeMedicationTiming(MedicationTiming timing) => timing switch
    {
        MedicationTiming.BeforeMedication => "antes da medicação",
        MedicationTiming.AfterMedication => "depois da medicação",
        MedicationTiming.NotApplicable => "não se aplica",
        _ => "medicação não informada"
    };

    private enum ConfirmationAction
    {
        None,
        DeleteMeasurement,
        DeletePatient
    }
}
