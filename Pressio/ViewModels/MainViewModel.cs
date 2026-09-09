
using System.Reactive;
using System.Reactive.Linq;
using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Pressio.Models;
using Pressio.Services;
using ReactiveUI;

namespace Pressio.ViewModels;

public class MainViewModel : ViewModelBase
{
    private enum SyncMode { Startup, Manual, Periodic }
    private const int SyncIntervalMinutes = 2;

    public MainViewModel(bool isMobileLayout = false)
    {
        IsMobileLayout = isMobileLayout;
        MeasurementForm.IsMobileLayout = isMobileLayout;
        PatientForm.IsMobileLayout = isMobileLayout;
        ReminderForm.IsMobileLayout = isMobileLayout;
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
    public bool IsProfileListDialogVisible => IsProfileListVisible && !IsMobileLayout;
    public bool IsProfileListMobilePageVisible => IsProfileListVisible && IsMobileLayout;
    public string PatientName => SelectedPatient?.Name ?? "Selecione um usuário";
    public string Initials => string.Concat(PatientName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => x[0])).ToUpperInvariant()[..Math.Min(2, PatientName.Length)];
    public string LastReading => Measurements.FirstOrDefault()?.DisplayValue ?? "—";
    public string LastReadingDetails => Measurements.FirstOrDefault() is { } measurement ? $"{measurement.DisplayDate}  •  {DescribeMedicationTiming(measurement.MedicationTiming)}" : "Nenhuma medição registrada";
    public string WeeklySummary => Measurements.Count == 0 ? "Registre a primeira medição" : $"{Measurements.Count} medições registradas";
    public string AverageReading => Measurements.Count == 0 ? "—" : BloodPressureMeasurement.Format((int)Math.Round(Measurements.Average(x => x.Systolic), MidpointRounding.AwayFromZero), (int)Math.Round(Measurements.Average(x => x.Diastolic), MidpointRounding.AwayFromZero));
    public string MeasurementCount => Measurements.Count.ToString();
    private Geometry _systolicLine = new StreamGeometry();
    public Geometry SystolicLine { get => _systolicLine; private set => this.RaiseAndSetIfChanged(ref _systolicLine, value); }
    private Geometry _diastolicLine = new StreamGeometry();
    public Geometry DiastolicLine { get => _diastolicLine; private set => this.RaiseAndSetIfChanged(ref _diastolicLine, value); }
    public ObservableCollection<ChartPointLabel> ChartLabels { get; } = new();
    public ObservableCollection<ChartPointMarker> ChartMarkers { get; } = new();
    public string BeforeMedicationSummary { get; private set; } = "—";
    public string AfterMedicationSummary { get; private set; } = "—";
    public IReadOnlyList<TimeSlotInfo> TimeDistribution { get; private set; } = Array.Empty<TimeSlotInfo>();
    public IReadOnlyList<ContextCountInfo> ContextCounts { get; private set; } = Array.Empty<ContextCountInfo>();
    public IReadOnlyList<CorrelationInfo> Correlations { get; private set; } = Array.Empty<CorrelationInfo>();
    public bool HasCorrelations => Correlations.Count > 0;
    public bool HasReadings => Measurements.Count > 0;
    public PressureCategory? LastReadingCategory => Measurements.FirstOrDefault()?.Category;
    public string LastReadingCategoryLabel => LastReadingCategory is { } c ? BloodPressureClassification.Label(c) : string.Empty;
    // Aviso de sincronização na abertura: mostra progresso e só exibe erro (sem mensagem de sucesso).
    public string SyncNotifier { get => _syncNotifier; private set => this.RaiseAndSetIfChanged(ref _syncNotifier, value); }
    public bool SyncNotifierIsError { get => _syncNotifierIsError; private set => this.RaiseAndSetIfChanged(ref _syncNotifierIsError, value); }
    public bool IsSyncNotifierVisible => !string.IsNullOrEmpty(SyncNotifier);
    public bool SyncInProgress => IsSyncNotifierVisible && !SyncNotifierIsError;

    private bool _isMeasurementFormVisible;
    private readonly MeasurementRepository _measurementRepository = new();
    private readonly SettingsRepository _settingsRepository = new();
    private readonly ReminderRepository _reminderRepository = new();
    private SyncService _syncService = null!;
    private readonly SupabaseClient _supabase = new();
    private Patient? _selectedPatient;
    private bool _isPatientFormVisible;
    private BloodPressureMeasurement? _selectedMeasurement;
    private bool _editingMeasurement;
    private bool _isSettingsVisible;
    private bool _isProfileListVisible;
    private long? _pendingDeletePatientId;
    private long? _editingPatientId;
    private bool _returnToProfileList;
    private bool _isAboutVisible;
    private bool _isAboutSplash = true;
    private bool _isConfirmDialogVisible;
    private string _confirmMessage = string.Empty;
    private ConfirmationAction _pendingConfirmation;
    private List<BloodPressureMeasurement> _sourceMeasurements = new();
    private string _filterPeriod = "Todo o histórico";
    private string _filterMedication = "Todas";
    private string _filterTimeOfDay = "Todos os horários";
    private string _filterSearch = string.Empty;
    private string _reportPeriod = "Todo o histórico";
    private DateTime? _reportStartDate = DateTime.Today.AddDays(-30);
    private DateTime? _reportEndDate = DateTime.Today;
    private bool _isRemindersVisible;
    private bool _isReminderFormVisible;
    private bool _editingReminder;
    private ReminderItem? _selectedReminder;
    private bool _isReminderNoticeVisible;
    private string _reminderNoticeMessage = string.Empty;
    private string _syncNotifier = string.Empty;
    private bool _syncNotifierIsError;
    private bool _isSplashVisible;
    private string _splashMessage = string.Empty;
    private bool _splashMinElapsed;
    private bool _splashSyncFinished;
    private bool _syncInProgress;
    private string _lastUploadedSnapshot = string.Empty;
    private readonly HashSet<(long Id, DateTime Date)> _firedReminders = new();
    private string _appVersion = string.Empty;
    private string _databaseSizeText = "—";
    private string _databasePathText = string.Empty;
    private string _lastSyncText = "Nunca";

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

    public MeasurementFormViewModel MeasurementForm { get; } = new();
    public SettingsViewModel Settings { get; } = new();
    public ProfileListViewModel ProfileList { get; } = new();
    public DialogService Dialog { get; } = new();
    public ObservableCollection<BloodPressureMeasurement> Measurements { get; } = new();
    public ObservableCollection<Patient> Patients { get; } = new();
    public Patient? SelectedPatient
    {
        get => _selectedPatient;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedPatient, value);
            if (value is not null) _settingsRepository.SaveLastPatientId(value.Id);
            ReloadMeasurements();
            this.RaisePropertyChanged(nameof(PatientName)); this.RaisePropertyChanged(nameof(Initials));
        }
    }
    public BloodPressureMeasurement? SelectedMeasurement { get => _selectedMeasurement; set => this.RaiseAndSetIfChanged(ref _selectedMeasurement, value); }
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
    public string NewPatientName => PatientForm.NewPatientName;
    public string PatientError => PatientForm.PatientError;
    public PatientFormViewModel PatientForm { get; } = new();
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
    public bool IsProfileListVisible
    {
        get => _isProfileListVisible;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isProfileListVisible, value);
            this.RaisePropertyChanged(nameof(IsProfileListDialogVisible));
            this.RaisePropertyChanged(nameof(IsProfileListMobilePageVisible));
        }
    }
    public bool IsAboutVisible { get => _isAboutVisible; private set { this.RaiseAndSetIfChanged(ref _isAboutVisible, value); this.RaisePropertyChanged(nameof(IsAboutDialogVisible)); this.RaisePropertyChanged(nameof(IsAboutMobilePageVisible)); } }
    public bool IsAboutDialogVisible => IsAboutVisible && !IsMobileLayout;
    public bool IsAboutMobilePageVisible => IsAboutVisible && IsMobileLayout;
    public bool IsAboutCloseVisible => !_isAboutSplash;
    public string AppVersion { get => _appVersion; private set => this.RaiseAndSetIfChanged(ref _appVersion, value); }
    public string DatabaseSizeText { get => _databaseSizeText; private set => this.RaiseAndSetIfChanged(ref _databaseSizeText, value); }
    public string DatabasePathText { get => _databasePathText; private set => this.RaiseAndSetIfChanged(ref _databasePathText, value); }
    public string LastSyncText { get => _lastSyncText; private set => this.RaiseAndSetIfChanged(ref _lastSyncText, value); }
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
    public IReadOnlyList<string> ReportPeriodOptions { get; } = new[] { "Todo o histórico", "Últimos 7 dias", "Últimos 30 dias", "Período personalizado" };
    public string ReportPeriod
    {
        get => _reportPeriod;
        set { if (_reportPeriod != value) { _reportPeriod = value; this.RaisePropertyChanged(nameof(ReportPeriod)); this.RaisePropertyChanged(nameof(IsCustomReportPeriod)); } }
    }
    public bool IsCustomReportPeriod => ReportPeriod == "Período personalizado";
    public DateTime? ReportStartDate { get => _reportStartDate; set => this.RaiseAndSetIfChanged(ref _reportStartDate, value); }
    public DateTime? ReportEndDate { get => _reportEndDate; set => this.RaiseAndSetIfChanged(ref _reportEndDate, value); }

    public IReadOnlyList<string> ChartPeriodOptions { get; } = new[] { "Hoje", "Últimos 7 dias", "Últimos 15 dias", "Últimos 30 dias" };
    private string _chartPeriod = "Últimos 30 dias";
    public string ChartPeriod
    {
        get => _chartPeriod;
        set
        {
            this.RaiseAndSetIfChanged(ref _chartPeriod, value);
            if (value is not null) RefreshDashboard();
        }
    }
    public ObservableCollection<ReminderItem> Reminders { get; } = new();
    public ReminderItem? SelectedReminder { get => _selectedReminder; set => this.RaiseAndSetIfChanged(ref _selectedReminder, value); }
    public bool IsRemindersVisible { get => _isRemindersVisible; private set { this.RaiseAndSetIfChanged(ref _isRemindersVisible, value); this.RaisePropertyChanged(nameof(IsRemindersDialogVisible)); this.RaisePropertyChanged(nameof(IsRemindersMobilePageVisible)); } }
    public bool IsRemindersDialogVisible => IsRemindersVisible && !IsMobileLayout;
    public bool IsRemindersMobilePageVisible => IsRemindersVisible && IsMobileLayout;
    public bool IsReminderFormVisible
    {
        get => _isReminderFormVisible;
        private set { this.RaiseAndSetIfChanged(ref _isReminderFormVisible, value); this.RaisePropertyChanged(nameof(IsReminderFormDialogVisible)); this.RaisePropertyChanged(nameof(IsReminderFormMobilePageVisible)); }
    }
    public bool IsReminderFormDialogVisible => IsReminderFormVisible && !IsMobileLayout;
    public bool IsReminderFormMobilePageVisible => IsReminderFormVisible && IsMobileLayout;
    public ReminderFormViewModel ReminderForm { get; } = new();
    public bool IsReminderNoticeVisible { get => _isReminderNoticeVisible; private set => this.RaiseAndSetIfChanged(ref _isReminderNoticeVisible, value); }
    public string ReminderNoticeMessage { get => _reminderNoticeMessage; set => this.RaiseAndSetIfChanged(ref _reminderNoticeMessage, value); }


    public ReactiveCommand<Unit, Unit> ShowMeasurementFormCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> CancelMeasurementCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> SaveMeasurementCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> DeleteMeasurementCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> EditMeasurementCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ShowPatientFormCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ShowProfileListCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ExportCsvCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ExportPdfCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ExportLetterCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> SendToHealthCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ShowReportOptionsCommand { get; private set; } = null!;
    public bool IsHealthExportAvailable => HealthExport.Service.IsSupported;
    public ReactiveCommand<Unit, Unit> BackupCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> RestoreCommand { get; private set; } = null!;
    public Interaction<ExportFileRequest, IStorageFile?> ExportFileInteraction { get; } = new();
    public Interaction<string, bool> OpenExportInteraction { get; } = new();
    public Interaction<Unit, string?> OpenFileInteraction { get; } = new();
    public Interaction<Unit, string?> FolderPickerInteraction { get; } = new();
    public Interaction<Unit, Unit> SyncNowInteraction { get; } = new();
    public ReactiveCommand<Unit, Unit> EditPatientCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> DeletePatientCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ShowSettingsCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ShowAboutCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> CloseAboutCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ConfirmDeleteCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> CancelDeleteCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ShowRemindersCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> CloseRemindersCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ShowReminderFormCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> CancelReminderFormCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> SaveReminderCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> EditReminderCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> DeleteReminderCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> DismissReminderNoticeCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ClearFiltersCommand { get; private set; } = null!;
    public ReactiveCommand<string, Unit> SetChartPeriodCommand { get; private set; } = null!;

    private void Initialize()
    {
        ShowMeasurementFormCommand = ReactiveCommand.Create(() =>
        {
            _editingMeasurement = false;
            MeasurementForm.IsEditMode = false;
            MeasurementForm.BloodPressureInput = string.Empty;
            MeasurementForm.Notes = string.Empty;
            MeasurementForm.MeasurementDate = DateTime.Today;
            MeasurementForm.MeasurementTime = DateTime.Now.TimeOfDay;
            MeasurementForm.MedicationTiming = MedicationTiming.NotInformed;
            MeasurementForm.HeartRateInput = string.Empty;
            MeasurementForm.AtRest = false;
            MeasurementForm.SelectedArm = "Não informado";
            MeasurementForm.SelectedPosition = "Não informado";
            MeasurementForm.SetContext(MeasurementContext.None);
            MeasurementForm.MeasurementError = string.Empty;
            IsMeasurementFormVisible = true;
            MeasurementForm.NotifyShown();
        });
        CancelMeasurementCommand = ReactiveCommand.Create(() =>
        {
            IsMeasurementFormVisible = false;
            MeasurementForm.BloodPressureInput = string.Empty;
            MeasurementForm.MeasurementError = string.Empty;
        });
        SaveMeasurementCommand = ReactiveCommand.Create(SaveMeasurement);
        DeleteMeasurementCommand = ReactiveCommand.Create(DeleteSelectedMeasurement);
        MeasurementForm.SaveRequested += SaveMeasurement;
        MeasurementForm.CancelRequested += () => { IsMeasurementFormVisible = false; };
        EditMeasurementCommand = ReactiveCommand.Create(EditSelectedMeasurement);
        ShowPatientFormCommand = ReactiveCommand.Create(ShowNewPatientForm);
        EditPatientCommand = ReactiveCommand.Create(EditPatient);
        PatientForm.SaveRequested += SavePatient;
        PatientForm.CancelRequested += ClosePatientForm;
        DeletePatientCommand = ReactiveCommand.Create(DeletePatient);
        ShowProfileListCommand = ReactiveCommand.Create(OpenProfileList);
        ProfileList.AddRequested += ShowNewPatientForm;
        ProfileList.EditRequested += EditPatient;
        ProfileList.DeleteRequested += DeletePatient;
        ProfileList.ActivateRequested += ActivatePatient;
        ProfileList.BackRequested += () => IsProfileListVisible = false;
        ShowSettingsCommand = ReactiveCommand.Create(() => { IsSettingsVisible = true; });
        Settings.ApplyRequested += ApplySettings;
        Settings.CancelRequested += () => { IsSettingsVisible = false; };
        Settings.BackupRequested += () => { _ = Backup(); };
        Settings.RestoreRequested += () => { _ = Restore(); };
        Settings.ChooseDirectoryRequested += () => { _ = ChooseSyncDirectory(); };
        Settings.SaveSupabaseRequested += SaveSupabaseConfig;
        Settings.SignInRequested += () => _ = SignInAsync();
        Settings.SignUpRequested += () => _ = SignUpAsync();
        Settings.SignOutRequested += () => _ = SignOut();
        Settings.SyncRequested += SyncNow;
        Settings.ForgotPasswordRequested += () => _ = RequestPasswordReset();
        _syncService = new SyncService(_measurementRepository, _reminderRepository, _settingsRepository, _settingsRepository.GetOrCreateSyncDeviceId());
        // Basear do snapshot local: compara com o último enviado para detectar alterações não sincronizadas.
        _lastUploadedSnapshot = _syncService.Serialize(_syncService.BuildLocalSnapshot());
        ShowAboutCommand = ReactiveCommand.Create(() => { _isAboutSplash = false; this.RaisePropertyChanged(nameof(IsAboutCloseVisible)); RefreshDiagnostics(); IsAboutVisible = true; });
        CloseAboutCommand = ReactiveCommand.Create(() => { IsAboutVisible = false; });
        CancelDeleteCommand = ReactiveCommand.Create(() => { IsConfirmDialogVisible = false; });
        ConfirmDeleteCommand = ReactiveCommand.Create(ExecuteConfirmedDelete);
        ClearFiltersCommand = ReactiveCommand.Create(() =>
        {
            FilterPeriod = "Todo o histórico";
            FilterMedication = "Todas";
            FilterTimeOfDay = "Todos os horários";
            FilterSearch = string.Empty;
        });
        SetChartPeriodCommand = ReactiveCommand.Create<string>(p => ChartPeriod = p);
        ShowRemindersCommand = ReactiveCommand.Create(() => { IsRemindersVisible = true; ReloadReminders(); });
        CloseRemindersCommand = ReactiveCommand.Create(() => { IsRemindersVisible = false; IsReminderFormVisible = false; });
        ShowReminderFormCommand = ReactiveCommand.Create(() =>
        {
            _editingReminder = false;
            ReminderForm.IsEditMode = false;
            ReminderForm.ReminderTime = DateTime.Now.TimeOfDay;
            ReminderForm.ReminderEnabled = true;
            ReminderForm.ReminderNote = string.Empty;
            ReminderForm.SetDays(ReminderDays.All);
            IsReminderFormVisible = true;
        });
        CancelReminderFormCommand = ReactiveCommand.Create(() => { IsReminderFormVisible = false; });
        SaveReminderCommand = ReactiveCommand.Create(SaveReminder);
        EditReminderCommand = ReactiveCommand.Create(EditReminder);
        DeleteReminderCommand = ReactiveCommand.Create(DeleteSelectedReminder);
        DismissReminderNoticeCommand = ReactiveCommand.Create(() => { IsReminderNoticeVisible = false; });
        ReminderForm.SaveRequested += SaveReminder;
        ReminderForm.CancelRequested += () => { IsReminderFormVisible = false; };
        try { Observable.Interval(TimeSpan.FromSeconds(20), RxApp.MainThreadScheduler).Subscribe(_ => CheckDueReminders()); } catch { }
        RescheduleEnabledReminders();
        _isAboutSplash = false;
        if (!IsMobileLayout)
        {
            _isAboutSplash = true;
            this.RaisePropertyChanged(nameof(IsAboutCloseVisible));
            RefreshDiagnostics();
            IsAboutVisible = true;
            Observable.Timer(TimeSpan.FromMilliseconds(1800), RxApp.MainThreadScheduler).Subscribe(_ => { _isAboutSplash = false; this.RaisePropertyChanged(nameof(IsAboutCloseVisible)); IsAboutVisible = false; });
        }
        ExportCsvCommand = ReactiveCommand.CreateFromTask(ExportCsv);
        ExportPdfCommand = ReactiveCommand.CreateFromTask(ExportPdf);
        ExportLetterCommand = ReactiveCommand.CreateFromTask(ExportLetter);
        SendToHealthCommand = ReactiveCommand.CreateFromTask(SendToHealth);
        ShowReportOptionsCommand = ReactiveCommand.CreateFromTask(ShowReportOptions);
        BackupCommand = ReactiveCommand.CreateFromTask(Backup);
        RestoreCommand = ReactiveCommand.CreateFromTask(Restore);
        LoadAppSettings();
        ReloadPatients();
        ShowSplash();
        if (_supabase.IsAuthenticated) _ = SyncCloudAsync(SyncMode.Startup);
        // Sincroniza automaticamente enquanto o app fica aberto (silencioso; mostra só erro).
        Observable.Interval(TimeSpan.FromMinutes(SyncIntervalMinutes), RxApp.MainThreadScheduler)
            .Subscribe(_tick => { if (_supabase.IsAuthenticated) _ = SyncCloudAsync(SyncMode.Periodic); });
    }

    private void ShowSplash()
    {
        if (!IsMobileLayout) return;
        IsSplashVisible = true;
        SplashMessage = _supabase.IsAuthenticated ? "Sincronizando dados com a nuvem…" : string.Empty;
        _splashMinElapsed = false;
        _splashSyncFinished = !_supabase.IsAuthenticated;
        Observable.Timer(TimeSpan.FromMilliseconds(1300), RxApp.MainThreadScheduler).Subscribe(_ =>
        {
            _splashMinElapsed = true;
            TryDismissSplash();
        });
        Observable.Timer(TimeSpan.FromMilliseconds(4500), RxApp.MainThreadScheduler).Subscribe(_ =>
        {
            TryDismissSplash(force: true);
        });
    }

    private void FinishStartupSplash()
    {
        _splashSyncFinished = true;
        TryDismissSplash();
    }

    private void TryDismissSplash(bool force = false)
    {
        if (!IsMobileLayout || !IsSplashVisible) return;
        if (force || (_splashMinElapsed && _splashSyncFinished)) IsSplashVisible = false;
    }

    private void LoadAppSettings()
    {
        Settings.SelectedAppearance = _settingsRepository.GetAppearance();
        Settings.SelectedPrimaryColor = _settingsRepository.GetPrimaryColor();
        Settings.SelectedDisplayFormat = _settingsRepository.GetMeasurementDisplayFormat();
        Settings.SyncDirectory = _settingsRepository.GetLastSyncDirectory() ?? string.Empty;
        Settings.SupabaseUrl = _settingsRepository.GetSupabaseUrl();
        Settings.SupabaseAnonKey = _settingsRepository.GetSupabaseAnonKey();
        _supabase.Configure(Settings.SupabaseUrl, Settings.SupabaseAnonKey);
        _supabase.RestoreSession(_settingsRepository.GetAuthSession());
        Settings.AuthEmail = _settingsRepository.GetRememberedEmail();
        Settings.IsAuthenticated = _supabase.IsAuthenticated;
        Settings.AuthUserEmail = _supabase.Email ?? string.Empty;
        BloodPressureMeasurement.UseShorthandFormat = Settings.SelectedDisplayFormat != "130/80";
        App.ApplyAppearance(Settings.SelectedAppearance, Settings.SelectedPrimaryColor);
    }

    private void ApplySettings()
    {
        App.ApplyAppearance(Settings.SelectedAppearance, Settings.SelectedPrimaryColor);
        _settingsRepository.SaveAppearance(Settings.SelectedAppearance, Settings.SelectedPrimaryColor);
        BloodPressureMeasurement.UseShorthandFormat = Settings.SelectedDisplayFormat != "130/80";
        _settingsRepository.SaveMeasurementDisplayFormat(Settings.SelectedDisplayFormat);
        IsSettingsVisible = false;
        ReloadMeasurements();
    }

    private async Task ChooseSyncDirectory()
    {
        var dir = await FolderPickerInteraction.Handle(Unit.Default).FirstAsync();
        if (string.IsNullOrWhiteSpace(dir)) return;
        _settingsRepository.SaveLastSyncDirectory(dir);
        Settings.SyncDirectory = dir;
    }

    private void SyncNow()
    {
        if (!_supabase.IsAuthenticated) { SetSyncError("Entre com sua conta (Configurações → Sincronização) para sincronizar na nuvem."); return; }
        _ = SyncCloudAsync(SyncMode.Manual);
    }

    private void SetSyncBanner(string text, bool isError)
    {
        SyncNotifier = text;
        SyncNotifierIsError = isError;
        this.RaisePropertyChanged(nameof(IsSyncNotifierVisible));
        this.RaisePropertyChanged(nameof(SyncInProgress));
    }

    private async Task SyncCloudAsync(SyncMode mode)
    {
        if (_syncInProgress) return;
        _syncInProgress = true;
        var isStartup = mode == SyncMode.Startup;
        var isManual = mode == SyncMode.Manual;
        // Só o modo abrir mostra o aviso de progresso; o periódico é silencioso (mostra erro só).
        if (isStartup) SetSyncBanner("Sincronizando dados com a nuvem…", isError: false);
        try
        {
            if (!_supabase.IsAuthenticated)
            {
                var msg = "Sessão inválida. Faça login novamente.";
                if (isManual) SetSyncError(msg); else { SetSyncBanner(msg, isError: true); FinishStartupSplash(); }
                return;
            }
            // Garante um access token válido (renova se o anterior expirou).
            var refreshed = await _supabase.RefreshAsync();
            if (!refreshed.Success)
            {
                var msg = "Sessão expirada. Faça login novamente.";
                if (isManual) SetSyncError(msg); else { SetSyncBanner(msg, isError: true); FinishStartupSplash(); }
                return;
            }
            _settingsRepository.SaveAuthSession(_supabase.SerializeSession());
            var remote = await _supabase.FetchSnapshotAsync();
            var mergedJson = ApplyRemoteSync(remote, showMessage: isManual);
            await _supabase.SaveSnapshotAsync(mergedJson);
            _lastUploadedSnapshot = mergedJson;
            _syncService.CompactTombstones();
            _settingsRepository.SaveLastSyncAtUtc(DateTimeOffset.UtcNow);
            LastSyncText = DateTimeOffset.UtcNow.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            if (isStartup) { FinishStartupSplash(); SetSyncBanner("", isError: false); }
        }
        catch (Exception ex)
        {
            var msg = "Falha ao sincronizar: " + ex.Message;
            if (isManual) ShowMessage(msg);
            else { SetSyncBanner(msg, isError: true); FinishStartupSplash(); }
        }
        finally
        {
            _syncInProgress = false;
        }
    }

    private void SaveSupabaseConfig()
    {
        _settingsRepository.SaveSupabase(Settings.SupabaseUrl, Settings.SupabaseAnonKey);
        _supabase.Configure(Settings.SupabaseUrl, Settings.SupabaseAnonKey);
        Notify("Dados da nuvem salvos.", "Sincronização");
    }

    private async Task SignOut()
    {
        // Verifica se há alterações locais que ainda não chegaram à nuvem (para avisar antes).
        var localSnapshot = _syncService.Serialize(_syncService.BuildLocalSnapshot());
        var hasUnsynced = !string.Equals(localSnapshot, _lastUploadedSnapshot, StringComparison.Ordinal);

        var message = hasUnsynced
            ? "Há alterações neste aparelho que ainda não foram sincronizadas com a nuvem. Ao sair, TODOS os dados do aparelho serão apagados e o que não está na nuvem se perderá. Deseja continuar?"
            : "Ao sair, os dados deste aparelho serão apagados (o que está na nuvem é mantido). Deseja continuar?";
        var confirm = await Dialog.ConfirmAsync("Sair e limpar este aparelho", message, "Sim, sair", "Cancelar");
        if (!confirm) return;

        // Cancela notificações dos lembretes antes de apagar.
        foreach (var reminder in _reminderRepository.GetAll())
            _ = Notifications.Service.CancelAsync(reminder.Id);
        // Apaga SOMENTE no dispositivo (DELETE real no banco local — NÃO gera tombstones,
        // então a nuvem não é afetada; ao entrar de novo o sync restaura).
        _measurementRepository.ClearAllData();
        _reminderRepository.ClearAllData();
        _settingsRepository.ClearAuthSession();
        _supabase.ClearSession();

        Settings.IsAuthenticated = false;
        Settings.AuthUserEmail = string.Empty;
        ReloadPatients();
        ReloadMeasurements();
        ReloadReminders();
        ReloadProfileList();
        Notify("Sessão encerrada e dados deste aparelho removidos.", "Sair");
    }

    private void ReloadProfileList()
    {
        ProfileList.Profiles.Clear();
    }

    private async Task RequestPasswordReset()
    {
        if (string.IsNullOrWhiteSpace(Settings.AuthEmail))
        {
            Notify("Informe seu e-mail para recuperar a senha.", "Recuperar senha");
            return;
        }
        var result = await _supabase.ResetPasswordAsync(Settings.AuthEmail);
        if (result.Success)
            Notify("Enviamos um link de recuperação para seu e-mail. Siga as instruções para criar uma nova senha e depois entre com ela.", "Recuperar senha");
        else
            Notify(result.Error ?? "Não foi possível enviar o link de recuperação.", "Recuperar senha");
    }

    private async Task SignUpAsync()
    {
        var result = await _supabase.SignUpAsync(Settings.AuthEmail, Settings.AuthPassword);
        if (result.Success && result.NeedsEmailConfirmation) Notify("Conta criada! Confirme seu e-mail e depois entre.", "Criar conta");
        else if (result.Success) ApplyAuth();
        else Notify(result.Error ?? "Não foi possível criar a conta.", "Criar conta");
    }

    private async Task SignInAsync()
    {
        var result = await _supabase.SignInAsync(Settings.AuthEmail, Settings.AuthPassword);
        if (result.Success) ApplyAuth();
        else Notify(result.Error ?? "Não foi possível entrar.", "Entrar");
    }

    private void ApplyAuth()
    {
        Settings.IsAuthenticated = _supabase.IsAuthenticated;
        Settings.AuthUserEmail = _supabase.Email ?? string.Empty;
        Notify("Conectado: " + Settings.AuthUserEmail, "Sincronização");
        _settingsRepository.SaveAuthSession(_supabase.SerializeSession());
        _settingsRepository.SaveRememberedEmail(Settings.AuthEmail);
        // Busca os dados da nuvem logo após entrar (restaura usuários/medições/lembretes).
        _ = SyncCloudAsync(SyncMode.Periodic);
    }



    public string BuildLocalSyncJson() => _syncService.Serialize(_syncService.BuildLocalSnapshot());

    private void RefreshDiagnostics()
    {
        AppVersion = typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        DatabasePathText = PressioDatabase.Path;
        try
        {
            var fi = new FileInfo(PressioDatabase.Path);
            DatabaseSizeText = fi.Exists ? FormatBytes(fi.Length) : "—";
        }
        catch
        {
            DatabaseSizeText = "—";
        }
        var lastSync = _settingsRepository.GetLastSyncAtUtc();
        LastSyncText = lastSync.HasValue ? lastSync.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "Nunca";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.#} {units[unit]}";
    }

    /// <summary>Mescla o local com o remoto (string JSON), aplica no banco e retorna o JSON mesclado.</summary>
    public string ApplyRemoteSync(string? remoteJson, bool showMessage = true)
    {
        var merged = _syncService.ApplyRemote(remoteJson);
        var msg = $"Sincronizado às {DateTime.Now:HH:mm} — {merged.Patients.Count} usuários, {merged.Measurements.Count} medições, {merged.Reminders.Count} lembretes.";
        ReloadPatients();
        ReloadMeasurements();
        ReloadReminders();
        RescheduleEnabledReminders();
        if (showMessage) ShowMessage(msg);
        return _syncService.Serialize(merged);
    }

    public void SetSyncError(string message) => ShowMessage(message);

    // Notificação in-app via DialogService (funciona em todas as plataformas, incl. mobile).
    public void ShowMessage(string message) => _ = Dialog.ShowInfoAsync("Pressio", message);
    public void Notify(string message, string title = "Pressio") => _ = Dialog.ShowInfoAsync(title, message);
    // Splash de abertura (mobile): tela de marca que some ao terminar a sincronização inicial.
    public bool IsSplashVisible { get => _isSplashVisible; private set => this.RaiseAndSetIfChanged(ref _isSplashVisible, value); }
    public string SplashMessage { get => _splashMessage; set { this.RaiseAndSetIfChanged(ref _splashMessage, value); this.RaisePropertyChanged(nameof(HasSplashMessage)); } }
    public bool HasSplashMessage => !string.IsNullOrEmpty(SplashMessage);

    private void SaveMeasurement()
    {
        if (!BloodPressureParser.TryParse(MeasurementForm.BloodPressureInput, out var parsed, out var error))
        {
            var msg = error ?? "Não foi possível interpretar a pressão.";
            MeasurementForm.MeasurementError = msg;
            Notify(msg, "Pressão inválida");
            return;
        }

        var measuredAt = (MeasurementForm.MeasurementDate ?? DateTime.Today).Date.Add(MeasurementForm.MeasurementTime ?? DateTime.Now.TimeOfDay);
        int? heartRate = null;
        var hrText = MeasurementForm.HeartRateInput?.Trim();
        if (!string.IsNullOrEmpty(hrText))
        {
            if (!int.TryParse(hrText, out var hr) || hr is < 20 or > 300) { var msg = "Frequência cardíaca inválida (20 a 300)."; MeasurementForm.MeasurementError = msg; Notify(msg, "Dados inválidos"); return; }
            heartRate = hr;
        }
        var context = MeasurementForm.SelectedContext();
        var measurement = new BloodPressureMeasurement(parsed!.Systolic, parsed.Diastolic, measuredAt, MeasurementForm.MedicationTiming, string.IsNullOrWhiteSpace(MeasurementForm.Notes) ? null : MeasurementForm.Notes.Trim(), context, heartRate, MeasurementForm.AtRest, MeasurementForm.ParseArm(), MeasurementForm.ParsePosition());
        if (_editingMeasurement && SelectedMeasurement is { Id: > 0 } existing)
        {
            measurement = measurement with { Id = existing.Id };
            _measurementRepository.Update(measurement);
        }
        else
        {
            if (SelectedPatient is null) { var msg = "Cadastre ou selecione um usuário antes de salvar."; MeasurementForm.MeasurementError = msg; Notify(msg, "Nenhum usuário"); return; }
            var id = _measurementRepository.Add(measurement, SelectedPatient.Id);
            measurement = measurement with { Id = id };
        }
        IsMeasurementFormVisible = false;
        MeasurementForm.BloodPressureInput = measurement.Systolic / 10d + "/" + measurement.Diastolic / 10d;
        MeasurementForm.MeasurementError = string.Empty;
        MeasurementForm.Notes = string.Empty;
        MeasurementForm.MeasurementDate = DateTime.Today;
        MeasurementForm.MeasurementTime = DateTime.Now.TimeOfDay;
        SelectedMeasurement = null;
        _editingMeasurement = false;
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
        if (string.IsNullOrWhiteSpace(PatientForm.NewPatientName)) { var msg = "Informe o nome do usuário."; PatientForm.PatientError = msg; Notify(msg, "Nome obrigatório"); return; }
        var name = PatientForm.NewPatientName.Trim();
        if (PatientForm.IsEditMode && _editingPatientId is { } pid)
        {
            var index = -1;
            for (var i = 0; i < Patients.Count; i++) if (Patients[i].Id == pid) { index = i; break; }
            if (index < 0) { IsPatientFormVisible = false; return; }
            var updated = Patients[index] with { Name = name };
            _measurementRepository.UpdatePatient(updated);
            Patients[index] = updated;
            if (SelectedPatient?.Id == pid) SelectedPatient = updated;
        }
        else
        {
            var id = _measurementRepository.AddPatient(name, null, null);
            var patient = new Patient(id, name);
            Patients.Add(patient);
            SelectedPatient = patient;
        }
        IsPatientFormVisible = false;
        if (_returnToProfileList)
        {
            _returnToProfileList = false;
            RefreshProfileList();
            IsProfileListVisible = true;
        }
        else if (IsProfileListVisible)
        {
            RefreshProfileList();
        }
    }

    private async Task Backup()
    {
        var file = await ExportFileInteraction.Handle(new ExportFileRequest($"pressio-backup-{DateTime.Now:yyyyMMdd-HHmmss}.db", ".db", "Backup", _settingsRepository.GetLastExportDirectory())).FirstAsync();
        if (file is null) { Notify("Backup cancelado."); return; }
        var path = TryLocalPath(file);
        try
        {
            if (File.Exists(path)) File.Delete(path);
            using var connection = new SqliteConnection($"Data Source={PressioDatabase.Path}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"VACUUM INTO '{path.Replace("'", "''")}'";
            command.ExecuteNonQuery();
            _settingsRepository.SaveLastExportDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            Notify("Backup criado com sucesso.");
        }
        catch (Exception ex)
        {
            Notify("Não foi possível criar o backup: " + ex.Message);
        }
    }

    private async Task Restore()
    {
        var path = await OpenFileInteraction.Handle(Unit.Default).FirstAsync();
        if (string.IsNullOrWhiteSpace(path)) { Notify("Restauração cancelada."); return; }
        try
        {
            File.Copy(path, PressioDatabase.Path, overwrite: true);
            ReloadPatients();
            ReloadMeasurements();
            ReloadReminders();
            LoadAppSettings();
            Notify("Backup restaurado com sucesso.");
        }
        catch (Exception ex)
        {
            Notify("Não foi possível restaurar o backup: " + ex.Message);
        }
    }

    private void ReloadPatients()
    {
        Patients.Clear();
        foreach (var patient in _measurementRepository.GetPatients()) Patients.Add(patient);
        var last = _settingsRepository.GetLastPatientId();
        SelectedPatient = last != 0 && Patients.FirstOrDefault(p => p.Id == last) is { } p ? p : Patients.FirstOrDefault();
        if (IsProfileListVisible) RefreshProfileList();
    }

    private async Task ShowReportOptions()
    {
        if (SelectedPatient is null || Measurements.Count == 0) { Notify("Não há medições para exportar."); return; }
        var options = new List<string> { "Carta ao médico", "Exportar PDF", "Exportar CSV" };
        var healthLabel = $"Enviar {SelectedPatient!.Name} para o Saúde";
        if (IsHealthExportAvailable) options.Add(healthLabel);
        var choice = await Dialog.ShowOptionsAsync("Relatório do usuário", options, "Fechar");
        switch (choice)
        {
            case "Carta ao médico": await ExportLetter(); break;
            case "Exportar PDF": await ExportPdf(); break;
            case "Exportar CSV": await ExportCsv(); break;
            case var c when c == healthLabel: await SendToHealth(); break;
        }
    }

    private async Task SendToHealth()
    {
        if (SelectedPatient is null || Measurements.Count == 0) { Notify("Não há medições para enviar."); return; }
        if (!HealthExport.Service.IsSupported) { Notify("Esta plataforma ainda não suporta o app Saúde."); return; }
        if (!await HealthExport.Service.RequestAuthorizationAsync())
        {
            Notify("Permissão de Saúde não concedida. Habilite em Ajustes → Saúde → Pressio.", "App Saúde");
            return;
        }
        var readings = Measurements
            .OrderBy(m => m.MeasuredAt)
            .Select(m => new HealthReading(m.Systolic, m.Diastolic, m.MeasuredAt, m.HeartRate))
            .ToList();
        var ok = await HealthExport.Service.ExportAsync(readings);
        Notify(ok
            ? $"{readings.Count} medição(ões) de {SelectedPatient!.Name} enviada(s) ao app Saúde."
            : "Não foi possível enviar as medições ao app Saúde.", "App Saúde");
    }

    private async Task ExportCsv()
    {        if (SelectedPatient is null || Measurements.Count == 0) { Notify("Não há medições para exportar."); return; }
        var (report, truncated) = BuildReportSet();
        if (report.Count == 0) { Notify("Não há medições no período selecionado."); return; }
        var file = await RequestExportPath("csv", "CSV");
        if (file is null) { Notify("Exportação cancelada."); return; }
        try
        {
            var rows = new[] { "Pressão;Data e hora;Medicação;Classificação;Contexto;Observação" }.Concat(report.Select(m =>
                $"{m.DisplayValue};{m.DisplayDate};{DescribeMedicationTiming(m.MedicationTiming)};{m.CategoryLabel};{(m.HasContext ? m.DisplayContext : "—")};{m.Notes?.Replace(';', ',') ?? string.Empty}"));
            var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, rows) + Environment.NewLine);
            var ok = await StorageWriter.Service.WriteAsync(file, bytes);
            if (!ok) throw new InvalidOperationException("Não foi possível gravar o arquivo.");
        }
        catch (Exception ex) { Notify("Falha ao salvar o CSV: " + ex.Message, "Exportar CSV"); return; }
        SaveExportDirectory(file);
        await ConfirmOpenExport(file);
    }

    private async Task ExportPdf()
    {
        if (SelectedPatient is null || Measurements.Count == 0) { Notify("Não há medições para exportar."); return; }
        var (report, truncated) = BuildReportSet();
        if (report.Count == 0) { Notify("Não há medições no período selecionado."); return; }
        var file = await RequestExportPath("pdf", "PDF");
        if (file is null) { Notify("Exportação cancelada."); return; }
        try
        {
            var ok = await ExportPdfBytes(file, SelectedPatient, report, ReportDescription(report), truncated);
            if (!ok) throw new InvalidOperationException("Não foi possível gravar o arquivo.");
        }
        catch (Exception ex) { Notify("Falha ao gerar o PDF: " + ex.Message, "Exportar PDF"); return; }
        SaveExportDirectory(file);
        await ConfirmOpenExport(file);
    }

    private async Task ExportLetter()
    {
        if (SelectedPatient is null || Measurements.Count == 0) { Notify("Não há medições para exportar."); return; }
        var (report, truncated) = BuildReportSet();
        if (report.Count == 0) { Notify("Não há medições no período selecionado."); return; }
        var file = await RequestExportPath("pdf", "PDF da carta");
        if (file is null) { Notify("Exportação cancelada."); return; }
        try
        {
            var ok = await ExportLetterBytes(file, SelectedPatient, report, ReportDescription(report));
            if (!ok) throw new InvalidOperationException("Não foi possível gravar o arquivo.");
        }
        catch (Exception ex) { Notify("Falha ao gerar a carta: " + ex.Message, "Carta ao médico"); return; }
        SaveExportDirectory(file);
        await ConfirmOpenExport(file);
    }

    private static async Task<bool> ExportPdfBytes(IStorageFile file, Patient patient, IReadOnlyList<BloodPressureMeasurement> report, string description, bool truncated)
    {
        using var ms = new MemoryStream();
        PdfReportService.Export(ms, patient, report, description, truncated);
        return await StorageWriter.Service.WriteAsync(file, ms.ToArray());
    }

    private static async Task<bool> ExportLetterBytes(IStorageFile file, Patient patient, IReadOnlyList<BloodPressureMeasurement> report, string description)
    {
        using var ms = new MemoryStream();
        PdfReportService.ExportDoctorLetter(ms, patient, report, description);
        return await StorageWriter.Service.WriteAsync(file, ms.ToArray());
    }

    // Pergunta se o usuário quer abrir o arquivo gerado com o aplicativo padrão do sistema
    // (funciona no desktop e no mobile; abre via Launcher).
    private async Task ConfirmOpenExport(IStorageFile file)
    {
        if (!await Dialog.ConfirmAsync("Abrir arquivo", "O arquivo foi gerado. Deseja abri-lo com o aplicativo padrão?", "Abrir", "Mais tarde")) return;
        var path = TryLocalPath(file);
        var ok = await OpenExportInteraction.Handle(path).FirstAsync();
        if (!ok) Notify("Não foi possível abrir o arquivo automaticamente.", "Abrir arquivo");
    }

    private (List<BloodPressureMeasurement> Items, bool Truncated) BuildReportSet()
        => DashboardCalculator.FilterByPeriod(Measurements, ReportPeriod, ReportStartDate, ReportEndDate);

    private string ReportDescription(IReadOnlyList<BloodPressureMeasurement> report)
    {
        var range = ReportPeriod switch
        {
            "Últimos 7 dias" => $"{DateTime.Today.AddDays(-6):dd/MM/yyyy} a {DateTime.Today:dd/MM/yyyy}",
            "Últimos 30 dias" => $"{DateTime.Today.AddDays(-29):dd/MM/yyyy} a {DateTime.Today:dd/MM/yyyy}",
            "Período personalizado" => $"{ReportStartDate?.ToString("dd/MM/yyyy") ?? "?"} a {ReportEndDate?.ToString("dd/MM/yyyy") ?? "?"}",
            _ => report.Count > 0 ? $"{report[0].MeasuredAt:dd/MM/yyyy} a {report[^1].MeasuredAt:dd/MM/yyyy}" : "—"
        };
        return $"Período do relatório: {range}";
    }

    private async Task<IStorageFile?> RequestExportPath(string extension, string kind)
    {
        var request = new ExportFileRequest($"pressio-{SelectedPatient!.Name.Replace(' ', '-')}-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}", $".{extension}", kind, _settingsRepository.GetLastExportDirectory());
        return await ExportFileInteraction.Handle(request).FirstAsync();
    }

    private void SaveExportDirectory(IStorageFile file)
    {
        var path = TryLocalPath(file);
        var directory = string.IsNullOrWhiteSpace(path) ? null : Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) _settingsRepository.SaveLastExportDirectory(directory);
    }

    private static string TryLocalPath(IStorageFile file)
        => file.TryGetLocalPath() ?? file.Path.LocalPath;

    private void ClosePatientForm()
    {
        IsPatientFormVisible = false;
        if (_returnToProfileList)
        {
            _returnToProfileList = false;
            RefreshProfileList();
            IsProfileListVisible = true;
        }
    }

    private void ShowNewPatientForm()
    {
        _editingPatientId = null;
        _returnToProfileList = true;
        IsProfileListVisible = false;
        PatientForm.IsEditMode = false;
        PatientForm.NewPatientName = string.Empty;
        PatientForm.PatientError = string.Empty;
        IsPatientFormVisible = true;
        PatientForm.NotifyShown();
    }

    private void OpenProfileList()
    {
        RefreshProfileList();
        IsProfileListVisible = true;
    }

    private void RefreshProfileList()
    {
        var activeId = SelectedPatient?.Id;
        ProfileList.Profiles.Clear();
        foreach (var patient in _measurementRepository.GetPatients())
            ProfileList.Profiles.Add(new PatientProfileItem(patient, patient.Id == activeId));
        ProfileList.SelectedProfile = ProfileList.Profiles.FirstOrDefault(x => x.IsActive) ?? ProfileList.Profiles.FirstOrDefault();
    }

    private void ActivatePatient()
    {
        if (ProfileList.SelectedProfile is not { } item) return;
        SelectedPatient = item.Patient;
        RefreshProfileList();
    }

    private void EditPatient()
    {
        if (ProfileList.SelectedProfile is not { } item) return;
        _editingPatientId = item.Patient.Id;
        _returnToProfileList = true;
        IsProfileListVisible = false;
        PatientForm.IsEditMode = true;
        PatientForm.NewPatientName = item.Patient.Name;
        PatientForm.PatientError = string.Empty;
        IsPatientFormVisible = true;
        PatientForm.NotifyShown();
    }

    private void DeletePatient()
    {
        if (ProfileList.SelectedProfile is not { } item) return;
        if (ProfileList.Profiles.Count <= 1)
        {
            ShowMessage("Mantenha ao menos um usuário cadastrado.");
            return;
        }
        _pendingDeletePatientId = item.Patient.Id;
        _pendingConfirmation = ConfirmationAction.DeletePatient;
        ConfirmMessage = $"Excluir o usuário \"{item.Patient.Name}\" e todas as suas medições?";
        IsConfirmDialogVisible = true;
    }

    private void ExecuteConfirmedDelete()
    {
        IsConfirmDialogVisible = false;
        try
        {
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
                    if (_pendingDeletePatientId is { } patientId)
                    {
                        _measurementRepository.DeletePatient(patientId);
                        ReloadPatients();
                        ReloadMeasurements();
                        RefreshProfileList();
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            Notify("Não foi possível concluir a ação: " + ex.Message, "Erro");
        }
        _pendingConfirmation = ConfirmationAction.None;
    }

    private void EditSelectedMeasurement()
    {
        if (SelectedMeasurement is not { } measurement) return;
        _editingMeasurement = true;
        MeasurementForm.IsEditMode = true;
        MeasurementForm.BloodPressureInput = $"{measurement.Systolic}/{measurement.Diastolic}";
        MeasurementForm.MeasurementDate = measurement.MeasuredAt.Date;
        MeasurementForm.MeasurementTime = measurement.MeasuredAt.TimeOfDay;
        MeasurementForm.MedicationTiming = measurement.MedicationTiming;
        MeasurementForm.Notes = measurement.Notes ?? string.Empty;
        MeasurementForm.HeartRateInput = measurement.HeartRate?.ToString() ?? string.Empty;
        MeasurementForm.AtRest = measurement.AtRest;
        MeasurementForm.SelectedArm = measurement.Arm switch { Arm.Right => "Direito", Arm.Left => "Esquerdo", _ => "Não informado" };
        MeasurementForm.SelectedPosition = measurement.Position switch { BodyPosition.Seated => "Sentado", BodyPosition.Lying => "Deitado", BodyPosition.Standing => "Em pé", _ => "Não informado" };
        MeasurementForm.SetContext(measurement.Context);
        MeasurementForm.MeasurementError = string.Empty;
        IsMeasurementFormVisible = true;
        MeasurementForm.NotifyShown();
    }

    private void ReloadMeasurements()
    {
        _sourceMeasurements = SelectedPatient is not null ? _measurementRepository.GetRecent(SelectedPatient.Id).ToList() : new();
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = MeasurementFilter.Apply(_sourceMeasurements, FilterPeriod, FilterMedication, FilterTimeOfDay, FilterSearch, DateTime.Today);
        Measurements.Clear();
        foreach (var measurement in filtered) Measurements.Add(measurement);
        RefreshDashboard();
    }

    private void RefreshDashboard()
    {
        var ordered = Measurements.OrderBy(x => x.MeasuredAt).ToList();
        BeforeMedicationSummary = DashboardCalculator.MedicationSummary(ordered, MedicationTiming.BeforeMedication);
        AfterMedicationSummary = DashboardCalculator.MedicationSummary(ordered, MedicationTiming.AfterMedication);
        TimeDistribution = DashboardCalculator.TimeDistribution(ordered);
        ContextCounts = DashboardCalculator.ContextCounts(ordered);
        Correlations = DashboardCalculator.Correlations(ordered);
        // O gráfico respeita o período selecionado (Hoje / 7 / 15 / 30 dias).
        var cutoff = ChartCutoff;
        var chartData = ordered.Where(m => m.MeasuredAt >= cutoff).ToList();
        if (chartData.Count == 0)
        {
            SystolicLine = new StreamGeometry();
            DiastolicLine = new StreamGeometry();
            ChartLabels.Clear();
            ChartMarkers.Clear();
        }
        else
        {
            var min = chartData.Min(x => Math.Min(x.Systolic, x.Diastolic));
            var max = Math.Max(min + 1, chartData.Max(x => Math.Max(x.Systolic, x.Diastolic)));
            double X(int i) => chartData.Count == 1 ? 250 : i * 500d / (chartData.Count - 1);
            double Y(int v) => 138 - ((v - min) * 108d / (max - min));
            var systolic = chartData.Select((x, i) => new Point(X(i), Y(x.Systolic))).ToList();
            var diastolic = chartData.Select((x, i) => new Point(X(i), Y(x.Diastolic))).ToList();
            SystolicLine = ChartPathBuilder.BuildSmooth(systolic);
            DiastolicLine = ChartPathBuilder.BuildSmooth(diastolic);
            ChartLabels.Clear();
            ChartMarkers.Clear();
            for (var i = 0; i < chartData.Count; i++)
            {
                ChartLabels.Add(new ChartPointLabel(BloodPressureMeasurement.Format(chartData[i].Systolic, chartData[i].Diastolic), (int)Math.Clamp(X(i) - 26, 4, 442), (int)Math.Clamp(Y(chartData[i].Systolic) - 26, 4, 134)));
                ChartMarkers.Add(new ChartPointMarker((int)X(i), (int)Y(chartData[i].Systolic), chartData[i].Category));
                ChartMarkers.Add(new ChartPointMarker((int)X(i), (int)Y(chartData[i].Diastolic), chartData[i].Category));
            }
        }

        this.RaisePropertyChanged(nameof(LastReading));
        this.RaisePropertyChanged(nameof(LastReadingDetails));
        this.RaisePropertyChanged(nameof(WeeklySummary));
        this.RaisePropertyChanged(nameof(AverageReading));
        this.RaisePropertyChanged(nameof(MeasurementCount));
        this.RaisePropertyChanged(nameof(BeforeMedicationSummary));
        this.RaisePropertyChanged(nameof(AfterMedicationSummary));
        this.RaisePropertyChanged(nameof(TimeDistribution));
        this.RaisePropertyChanged(nameof(ContextCounts));
        this.RaisePropertyChanged(nameof(Correlations));
        this.RaisePropertyChanged(nameof(HasCorrelations));
        this.RaisePropertyChanged(nameof(HasReadings));
        this.RaisePropertyChanged(nameof(LastReadingCategory));
        this.RaisePropertyChanged(nameof(LastReadingCategoryLabel));
    }

    private DateTime ChartCutoff => ChartPeriod switch
    {
        "Hoje" => DateTime.Today,
        "Últimos 7 dias" => DateTime.Today.AddDays(-6),
        "Últimos 15 dias" => DateTime.Today.AddDays(-14),
        _ => DateTime.Today.AddDays(-29)
    };

    private void ReloadReminders()
    {
        Reminders.Clear();
        foreach (var reminder in _reminderRepository.GetAll())
            Reminders.Add(new ReminderItem(reminder, PersistReminderEnabled));
    }

    private void PersistReminderEnabled(ReminderItem item)
    {
        _reminderRepository.Update(new Reminder(item.Id, item.Time, item.Days, item.Enabled, item.Note));
        if (item.Enabled) _ = Notifications.Service.ScheduleAsync(new Reminder(item.Id, item.Time, item.Days, item.Enabled, item.Note));
        else _ = Notifications.Service.CancelAsync(item.Id);
    }

    private void SaveReminder()
    {
        var time = ReminderForm.ReminderTime ?? DateTime.Now.TimeOfDay;
        var days = ReminderForm.SelectedDays();
        if (days == ReminderDays.None) days = ReminderDays.All;
        var note = string.IsNullOrWhiteSpace(ReminderForm.ReminderNote) ? null : ReminderForm.ReminderNote.Trim();
        if (_editingReminder && SelectedReminder is { } selected)
        {
            var updated = new Reminder(selected.Id, time, days, ReminderForm.ReminderEnabled, note);
            _reminderRepository.Update(updated);
            var index = Reminders.IndexOf(selected);
            Reminders[index] = new ReminderItem(updated, PersistReminderEnabled);
            SelectedReminder = Reminders[index];
            if (updated.Enabled) _ = Notifications.Service.ScheduleAsync(updated);
            else _ = Notifications.Service.CancelAsync(updated.Id);
        }
        else
        {
            var id = _reminderRepository.Add(new Reminder(0, time, days, ReminderForm.ReminderEnabled, note));
            var reminder = new Reminder(id, time, days, ReminderForm.ReminderEnabled, note);
            var item = new ReminderItem(reminder, PersistReminderEnabled);
            Reminders.Add(item);
            SelectedReminder = item;
            if (reminder.Enabled) _ = Notifications.Service.ScheduleAsync(reminder);
        }
        IsReminderFormVisible = false;
    }

    private void EditReminder()
    {
        if (SelectedReminder is not { } item) return;
        _editingReminder = true;
        ReminderForm.IsEditMode = true;
        ReminderForm.ReminderTime = item.Time;
        ReminderForm.ReminderEnabled = item.Enabled;
        ReminderForm.ReminderNote = item.Note ?? string.Empty;
        ReminderForm.SetDays(item.Days);
        IsReminderFormVisible = true;
    }

    private void DeleteSelectedReminder()
    {
        if (SelectedReminder is not { Id: > 0 } item) return;
        _reminderRepository.Delete(item.Id);
        _ = Notifications.Service.CancelAsync(item.Id);
        Reminders.Remove(item);
        SelectedReminder = null;
    }

    private void RescheduleEnabledReminders()
    {
        foreach (var reminder in _reminderRepository.GetAll())
            if (reminder.Enabled) _ = Notifications.Service.ScheduleAsync(reminder);
    }

    private void CheckDueReminders()
    {
        var now = DateTime.Now;
        foreach (var item in Reminders)
        {
            if (!ReminderDueCalculator.IsDue(item.Enabled, item.Days, item.Time, now)) continue;
            if (!_firedReminders.Add((item.Id, now.Date))) continue;
            var message = "" + item.DisplayTime + " — hora de aferir a pressão" + (string.IsNullOrWhiteSpace(item.Note) ? "" : "\n" + item.Note);
            ReminderNoticeMessage = message;
            IsReminderNoticeVisible = true;
            if (!Notifications.Service.SupportsScheduledNotifications)
                _ = Notifications.Service.ShowNowAsync("Pressio", message);
        }
    }

    // Fecha o overlay/página mais interno (usado pela navegação, ex.: botão voltar do Android).
    // Retorna true se consumiu o evento; false quando não há o que fechar (a aplicação sai).
    public bool HandleBack()
    {
        if (IsReminderNoticeVisible) { IsReminderNoticeVisible = false; return true; }
        if (IsConfirmDialogVisible) { IsConfirmDialogVisible = false; return true; }
        if (IsReminderFormVisible) { IsReminderFormVisible = false; return true; }
        if (IsRemindersVisible) { IsRemindersVisible = false; return true; }
        if (IsAboutVisible) { IsAboutVisible = false; return true; }
        if (IsSettingsVisible) { IsSettingsVisible = false; return true; }
        if (IsProfileListVisible) { IsProfileListVisible = false; return true; }
        if (IsPatientFormVisible) { IsPatientFormVisible = false; return true; }
        if (IsMeasurementFormVisible) { IsMeasurementFormVisible = false; return true; }
        return false;
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
