
using System.Reactive;
using System.Reactive.Linq;
using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Avalonia;
using Avalonia.Media;
using Pressio.Models;
using Pressio.Services;
using ReactiveUI;

namespace Pressio.ViewModels;

public class MainViewModel : ViewModelBase
{
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
    public string BeforeMedicationSummary { get; private set; } = "—";
    public string AfterMedicationSummary { get; private set; } = "—";
    public IReadOnlyList<TimeSlotInfo> TimeDistribution { get; private set; } = Array.Empty<TimeSlotInfo>();
    public IReadOnlyList<ContextCountInfo> ContextCounts { get; private set; } = Array.Empty<ContextCountInfo>();
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
    private string _exportStatus = string.Empty;
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
    private string _message = string.Empty;
    private bool _isMessageVisible;
    private string _syncNotifier = string.Empty;
    private bool _syncNotifierIsError;
    private bool _isSplashVisible;
    private string _splashMessage = string.Empty;
    private bool _splashMinElapsed;
    private bool _splashSyncFinished;
    private readonly HashSet<(long Id, DateTime Date)> _firedReminders = new();

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
    public string ExportStatus { get => _exportStatus; private set => this.RaiseAndSetIfChanged(ref _exportStatus, value); }
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
    public ReactiveCommand<Unit, Unit> BackupCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> RestoreCommand { get; private set; } = null!;
    public Interaction<ExportFileRequest, string?> ExportFileInteraction { get; } = new();
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
    public ReactiveCommand<Unit, Unit> DismissMessageCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ClearFiltersCommand { get; private set; } = null!;

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
        Settings.SignOutRequested += SignOut;
        Settings.SyncRequested += SyncNow;
        _syncService = new SyncService(_measurementRepository, _reminderRepository, _settingsRepository, _settingsRepository.GetOrCreateSyncDeviceId());
        ShowAboutCommand = ReactiveCommand.Create(() => { _isAboutSplash = false; this.RaisePropertyChanged(nameof(IsAboutCloseVisible)); IsAboutVisible = true; });
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
        DismissMessageCommand = ReactiveCommand.Create(() => { IsMessageVisible = false; });
        ReminderForm.SaveRequested += SaveReminder;
        ReminderForm.CancelRequested += () => { IsReminderFormVisible = false; };
        try { Observable.Interval(TimeSpan.FromSeconds(20), RxApp.MainThreadScheduler).Subscribe(_ => CheckDueReminders()); } catch { }
        RescheduleEnabledReminders();
        _isAboutSplash = false;
        if (!IsMobileLayout)
        {
            _isAboutSplash = true;
            this.RaisePropertyChanged(nameof(IsAboutCloseVisible));
            IsAboutVisible = true;
            Observable.Timer(TimeSpan.FromMilliseconds(1800), RxApp.MainThreadScheduler).Subscribe(_ => { _isAboutSplash = false; this.RaisePropertyChanged(nameof(IsAboutCloseVisible)); IsAboutVisible = false; });
        }
        ExportCsvCommand = ReactiveCommand.CreateFromTask(ExportCsv);
        ExportPdfCommand = ReactiveCommand.CreateFromTask(ExportPdf);
        ExportLetterCommand = ReactiveCommand.CreateFromTask(ExportLetter);
        BackupCommand = ReactiveCommand.CreateFromTask(Backup);
        RestoreCommand = ReactiveCommand.CreateFromTask(Restore);
        LoadAppSettings();
        ReloadPatients();
        ShowSplash();
        if (_supabase.IsAuthenticated) _ = SyncCloudAsync(showResult: false);
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
        Settings.SyncStatus = string.Empty;
    }

    private void SyncNow()
    {
        if (!_supabase.IsAuthenticated) { SetSyncError("Entre com sua conta (Configurações → Sincronização) para sincronizar na nuvem."); return; }
        _ = SyncCloudAsync(showResult: true);
    }

    private void SetSyncBanner(string text, bool isError)
    {
        SyncNotifier = text;
        SyncNotifierIsError = isError;
        this.RaisePropertyChanged(nameof(IsSyncNotifierVisible));
        this.RaisePropertyChanged(nameof(SyncInProgress));
    }

    private async Task SyncCloudAsync(bool showResult)
    {
        var startup = !showResult;
        if (startup) SetSyncBanner("Sincronizando dados com a nuvem…", isError: false);
        try
        {
            if (!_supabase.IsAuthenticated)
            {
                var msg = "Sessão inválida. Faça login novamente.";
                if (showResult) SetSyncError(msg); else { SetSyncBanner(msg, isError: true); FinishStartupSplash(); }
                return;
            }
            // Garante um access token válido (renova se o anterior expirou).
            var refreshed = await _supabase.RefreshAsync();
            if (!refreshed.Success)
            {
                var msg = "Sessão expirada. Faça login novamente.";
                if (showResult) SetSyncError(msg); else { SetSyncBanner(msg, isError: true); FinishStartupSplash(); }
                return;
            }
            _settingsRepository.SaveAuthSession(_supabase.SerializeSession());
            var remote = await _supabase.FetchSnapshotAsync();
            var mergedJson = ApplyRemoteSync(remote, showMessage: showResult);
            await _supabase.SaveSnapshotAsync(mergedJson);
            _syncService.CompactTombstones();
            if (startup) { FinishStartupSplash(); SetSyncBanner("", isError: false); }
        }
        catch (Exception ex)
        {
            Settings.SyncStatus = "Falha ao sincronizar: " + ex.Message;
            if (showResult) ShowMessage(Settings.SyncStatus);
            else { SetSyncBanner(Settings.SyncStatus, isError: true); FinishStartupSplash(); }
        }
    }

    private void SaveSupabaseConfig()
    {
        _settingsRepository.SaveSupabase(Settings.SupabaseUrl, Settings.SupabaseAnonKey);
        _supabase.Configure(Settings.SupabaseUrl, Settings.SupabaseAnonKey);
        Settings.SyncStatus = "Dados da nuvem salvos.";
    }

    private async Task SignUpAsync()
    {
        var result = await _supabase.SignUpAsync(Settings.AuthEmail, Settings.AuthPassword);
        if (result.Success && result.NeedsEmailConfirmation) Settings.SyncStatus = "Conta criada! Confirme seu e-mail e depois entre.";
        else if (result.Success) ApplyAuth();
        else Settings.SyncStatus = result.Error ?? "Não foi possível criar a conta.";
    }

    private async Task SignInAsync()
    {
        var result = await _supabase.SignInAsync(Settings.AuthEmail, Settings.AuthPassword);
        if (result.Success) ApplyAuth();
        else Settings.SyncStatus = result.Error ?? "Não foi possível entrar.";
    }

    private void ApplyAuth()
    {
        Settings.IsAuthenticated = _supabase.IsAuthenticated;
        Settings.AuthUserEmail = _supabase.Email ?? string.Empty;
        Settings.SyncStatus = "Conectado: " + Settings.AuthUserEmail;
        _settingsRepository.SaveAuthSession(_supabase.SerializeSession());
        _settingsRepository.SaveRememberedEmail(Settings.AuthEmail);
    }

    private void SignOut()
    {
        _supabase.ClearSession();
        _settingsRepository.ClearAuthSession();
        Settings.IsAuthenticated = false;
        Settings.AuthUserEmail = string.Empty;
        Settings.SyncStatus = "Sessão encerrada.";
    }

    public string BuildLocalSyncJson() => _syncService.Serialize(_syncService.BuildLocalSnapshot());

    /// <summary>Mescla o local com o remoto (string JSON), aplica no banco e retorna o JSON mesclado.</summary>
    public string ApplyRemoteSync(string? remoteJson, bool showMessage = true)
    {
        var merged = _syncService.ApplyRemote(remoteJson);
        Settings.SyncStatus = $"Sincronizado às {DateTime.Now:HH:mm} — {merged.Patients.Count} usuários, {merged.Measurements.Count} medições, {merged.Reminders.Count} lembretes.";
        ReloadPatients();
        ReloadMeasurements();
        ReloadReminders();
        RescheduleEnabledReminders();
        if (showMessage) ShowMessage(Settings.SyncStatus);
        return _syncService.Serialize(merged);
    }

    public void SetSyncError(string message)
    {
        Settings.SyncStatus = message;
        ShowMessage(message);
    }

    // Overlay in-app de mensagem (funciona em mobile; Window.ShowDialog não existe em iOS).
    public void ShowMessage(string message) { Message = message; IsMessageVisible = true; }
    public string Message { get => _message; set => this.RaiseAndSetIfChanged(ref _message, value); }
    public bool IsMessageVisible { get => _isMessageVisible; set => this.RaiseAndSetIfChanged(ref _isMessageVisible, value); }
    // Splash de abertura (mobile): tela de marca que some ao terminar a sincronização inicial.
    public bool IsSplashVisible { get => _isSplashVisible; private set => this.RaiseAndSetIfChanged(ref _isSplashVisible, value); }
    public string SplashMessage { get => _splashMessage; set { this.RaiseAndSetIfChanged(ref _splashMessage, value); this.RaisePropertyChanged(nameof(HasSplashMessage)); } }
    public bool HasSplashMessage => !string.IsNullOrEmpty(SplashMessage);

    private void SaveMeasurement()
    {
        if (!BloodPressureParser.TryParse(MeasurementForm.BloodPressureInput, out var parsed, out var error))
        {
            MeasurementForm.MeasurementError = error ?? "Não foi possível interpretar a pressão.";
            return;
        }

        var measuredAt = (MeasurementForm.MeasurementDate ?? DateTime.Today).Date.Add(MeasurementForm.MeasurementTime ?? DateTime.Now.TimeOfDay);
        int? heartRate = null;
        var hrText = MeasurementForm.HeartRateInput?.Trim();
        if (!string.IsNullOrEmpty(hrText))
        {
            if (!int.TryParse(hrText, out var hr) || hr is < 20 or > 300) { MeasurementForm.MeasurementError = "Frequência cardíaca inválida (20 a 300)."; return; }
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
            if (SelectedPatient is null) { MeasurementForm.MeasurementError = "Cadastre ou selecione um usuário antes de salvar."; return; }
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
        if (string.IsNullOrWhiteSpace(PatientForm.NewPatientName)) { PatientForm.PatientError = "Informe o nome do usuário."; return; }
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
        var path = await ExportFileInteraction.Handle(new ExportFileRequest($"pressio-backup-{DateTime.Now:yyyyMMdd-HHmmss}.db", ".db", "Backup", _settingsRepository.GetLastExportDirectory())).FirstAsync();
        if (string.IsNullOrWhiteSpace(path)) { Settings.ExportStatus = "Backup cancelado."; return; }
        try
        {
            if (File.Exists(path)) File.Delete(path);
            using var connection = new SqliteConnection($"Data Source={PressioDatabase.Path}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"VACUUM INTO '{path.Replace("'", "''")}'";
            command.ExecuteNonQuery();
            _settingsRepository.SaveLastExportDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            ExportStatus = $"Backup criado em: {path}";
        }
        catch (Exception ex)
        {
            Settings.ExportStatus = "Não foi possível criar o backup: " + ex.Message;
        }
    }

    private async Task Restore()
    {
        var path = await OpenFileInteraction.Handle(Unit.Default).FirstAsync();
        if (string.IsNullOrWhiteSpace(path)) { Settings.ExportStatus = "Restauração cancelada."; return; }
        try
        {
            File.Copy(path, PressioDatabase.Path, overwrite: true);
            ReloadPatients();
            ReloadMeasurements();
            ReloadReminders();
            LoadAppSettings();
            Settings.ExportStatus = "Backup restaurado com sucesso.";
        }
        catch (Exception ex)
        {
            Settings.ExportStatus = "Não foi possível restaurar o backup: " + ex.Message;
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

    private async Task ExportCsv()
    {        if (SelectedPatient is null || Measurements.Count == 0) { ExportStatus = "Não há medições para exportar."; return; }
        var (report, truncated) = BuildReportSet();
        var path = await RequestExportPath("csv", "CSV");
        if (path is null) { ExportStatus = "Exportação cancelada."; return; }
        var rows = new[] { "Pressão;Data e hora;Medicação;Classificação;Contexto;Observação" }.Concat(report.Select(m =>
            $"{m.DisplayValue};{m.DisplayDate};{DescribeMedicationTiming(m.MedicationTiming)};{m.CategoryLabel};{(m.HasContext ? m.DisplayContext : "—")};{m.Notes?.Replace(';', ',') ?? string.Empty}"));
        File.WriteAllLines(path, rows);
        SaveExportDirectory(path);
        ExportStatus = $"Relatório CSV salvo em: {path}{(truncated ? " (últimos 30 registros)" : "")}";
        await ConfirmOpenExport(path);
    }

    private async Task ExportPdf()
    {
        if (SelectedPatient is null || Measurements.Count == 0) { ExportStatus = "Não há medições para exportar."; return; }
        var (report, truncated) = BuildReportSet();
        var path = await RequestExportPath("pdf", "PDF");
        if (path is null) { ExportStatus = "Exportação cancelada."; return; }
        PdfReportService.Export(path, SelectedPatient, report, ReportDescription(report), truncated);
        SaveExportDirectory(path);
        ExportStatus = $"Relatório PDF salvo em: {path}{(truncated ? " (últimos 30 registros)" : "")}";
        await ConfirmOpenExport(path);
    }

    private async Task ExportLetter()
    {
        if (SelectedPatient is null || Measurements.Count == 0) { ExportStatus = "Não há medições para exportar."; return; }
        var (report, truncated) = BuildReportSet();
        var path = await RequestExportPath("pdf", "PDF da carta");
        if (path is null) { ExportStatus = "Exportação cancelada."; return; }
        PdfReportService.ExportDoctorLetter(path, SelectedPatient, report, ReportDescription(report));
        SaveExportDirectory(path);
        ExportStatus = $"Carta ao médico salva em: {path}{(truncated ? " (últimos 30 registros)" : "")}";
        await ConfirmOpenExport(path);
    }

    // Pergunta se o usuário quer abrir o arquivo gerado com o aplicativo padrão do sistema
    // (funciona no desktop e no mobile; abre via Launcher).
    private async Task ConfirmOpenExport(string path)
    {
        if (!await Dialog.ConfirmAsync("Abrir arquivo", "O arquivo foi gerado. Deseja abri-lo com o aplicativo padrão?", "Abrir", "Mais tarde")) return;
        var ok = await OpenExportInteraction.Handle(path).FirstAsync();
        if (!ok) ExportStatus = "Não foi possível abrir o arquivo automaticamente.";
    }

    private (List<BloodPressureMeasurement> Items, bool Truncated) BuildReportSet()
    {
        IEnumerable<BloodPressureMeasurement> query = Measurements;
        switch (ReportPeriod)
        {
            case "Últimos 7 dias":
                var from7 = DateTime.Today.AddDays(-6);
                query = query.Where(m => m.MeasuredAt.Date >= from7);
                break;
            case "Últimos 30 dias":
                var from30 = DateTime.Today.AddDays(-29);
                query = query.Where(m => m.MeasuredAt.Date >= from30);
                break;
            case "Período personalizado":
                if (ReportStartDate is { } start) query = query.Where(m => m.MeasuredAt.Date >= start.Date);
                if (ReportEndDate is { } end) query = query.Where(m => m.MeasuredAt.Date <= end.Date);
                break;
        }
        var list = query.OrderByDescending(m => m.MeasuredAt).ToList();
        var truncated = list.Count > 30;
        if (truncated) list = list.Take(30).ToList();
        return (list.OrderBy(m => m.MeasuredAt).ToList(), truncated);
    }

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

    private async Task<string?> RequestExportPath(string extension, string kind)
    {
        var request = new ExportFileRequest($"pressio-{SelectedPatient!.Name.Replace(' ', '-')}-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}", $".{extension}", kind, _settingsRepository.GetLastExportDirectory());
        return await ExportFileInteraction.Handle(request).FirstAsync();
    }

    private void SaveExportDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) _settingsRepository.SaveLastExportDirectory(directory);
    }

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
        var ordered = Measurements.OrderBy(x => x.MeasuredAt).ToList();
        BeforeMedicationSummary = SummarizeByMedication(ordered, MedicationTiming.BeforeMedication);
        AfterMedicationSummary = SummarizeByMedication(ordered, MedicationTiming.AfterMedication);
        TimeDistribution = BuildTimeDistribution(ordered);
        ContextCounts = BuildContextCounts(ordered);
        if (ordered.Count == 0)
        {
            SystolicLine = new StreamGeometry();
            DiastolicLine = new StreamGeometry();
            ChartLabels.Clear();
        }
        else
        {
            var min = ordered.Min(x => Math.Min(x.Systolic, x.Diastolic));
            var max = Math.Max(min + 1, ordered.Max(x => Math.Max(x.Systolic, x.Diastolic)));
            double X(int i) => ordered.Count == 1 ? 250 : i * 500d / (ordered.Count - 1);
            double Y(int v) => 138 - ((v - min) * 108d / (max - min));
            var systolic = ordered.Select((x, i) => new Point(X(i), Y(x.Systolic))).ToList();
            var diastolic = ordered.Select((x, i) => new Point(X(i), Y(x.Diastolic))).ToList();
            SystolicLine = ChartPathBuilder.BuildSmooth(systolic);
            DiastolicLine = ChartPathBuilder.BuildSmooth(diastolic);
            ChartLabels.Clear();
            for (var i = 0; i < ordered.Count; i++)
                ChartLabels.Add(new ChartPointLabel(BloodPressureMeasurement.Format(ordered[i].Systolic, ordered[i].Diastolic), (int)Math.Clamp(X(i) - 26, 4, 442), (int)Math.Clamp(Y(ordered[i].Systolic) - 26, 4, 134)));
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
        this.RaisePropertyChanged(nameof(HasReadings));
        this.RaisePropertyChanged(nameof(LastReadingCategory));
        this.RaisePropertyChanged(nameof(LastReadingCategoryLabel));
    }

    private static string SummarizeByMedication(IReadOnlyList<BloodPressureMeasurement> items, MedicationTiming timing)
    {
        var subset = items.Where(x => x.MedicationTiming == timing).ToList();
        if (subset.Count == 0) return "—";
        var systolic = (int)Math.Round(subset.Average(x => x.Systolic), MidpointRounding.AwayFromZero);
        var diastolic = (int)Math.Round(subset.Average(x => x.Diastolic), MidpointRounding.AwayFromZero);
        return $"{subset.Count}x  ·  média {BloodPressureMeasurement.Format(systolic, diastolic)}";
    }

    private static IReadOnlyList<TimeSlotInfo> BuildTimeDistribution(IReadOnlyList<BloodPressureMeasurement> items) => new[]
    {
        BuildSlot("Madrugada", items.Where(x => x.MeasuredAt.Hour < 6)),
        BuildSlot("Manhã", items.Where(x => x.MeasuredAt.Hour >= 6 && x.MeasuredAt.Hour < 12)),
        BuildSlot("Tarde", items.Where(x => x.MeasuredAt.Hour >= 12 && x.MeasuredAt.Hour < 18)),
        BuildSlot("Noite", items.Where(x => x.MeasuredAt.Hour >= 18)),
    };

    private static TimeSlotInfo BuildSlot(string label, IEnumerable<BloodPressureMeasurement> subset)
    {
        var list = subset.ToList();
        if (list.Count == 0) return new TimeSlotInfo(label, 0, "—");
        var systolic = (int)Math.Round(list.Average(x => x.Systolic), MidpointRounding.AwayFromZero);
        var diastolic = (int)Math.Round(list.Average(x => x.Diastolic), MidpointRounding.AwayFromZero);
        return new TimeSlotInfo(label, list.Count, BloodPressureMeasurement.Format(systolic, diastolic));
    }

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
            _ = Notifications.Service.ScheduleAsync(updated);
        }
        else
        {
            var id = _reminderRepository.Add(new Reminder(0, time, days, ReminderForm.ReminderEnabled, note));
            var reminder = new Reminder(id, time, days, ReminderForm.ReminderEnabled, note);
            var item = new ReminderItem(reminder, PersistReminderEnabled);
            Reminders.Add(item);
            SelectedReminder = item;
            _ = Notifications.Service.ScheduleAsync(reminder);
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
        var dayFlag = NowToReminderDay(now.DayOfWeek);
        foreach (var item in Reminders)
        {
            if (!item.Enabled || item.Days == ReminderDays.None || (item.Days & dayFlag) == 0) continue;
            if (Math.Abs((item.Time - now.TimeOfDay).TotalMinutes) >= 1) continue;
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
        if (IsPatientFormVisible) { IsPatientFormVisible = false; return true; }
        if (IsMeasurementFormVisible) { IsMeasurementFormVisible = false; return true; }
        return false;
    }

    private static ReminderDays NowToReminderDay(DayOfWeek day) => day switch
    {
        DayOfWeek.Sunday => ReminderDays.Sunday,
        DayOfWeek.Monday => ReminderDays.Monday,
        DayOfWeek.Tuesday => ReminderDays.Tuesday,
        DayOfWeek.Wednesday => ReminderDays.Wednesday,
        DayOfWeek.Thursday => ReminderDays.Thursday,
        DayOfWeek.Friday => ReminderDays.Friday,
        DayOfWeek.Saturday => ReminderDays.Saturday,
        _ => ReminderDays.None
    };

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
