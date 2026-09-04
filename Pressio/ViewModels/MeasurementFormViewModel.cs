using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using Pressio.Models;
using ReactiveUI;

namespace Pressio.ViewModels;

public sealed class MeasurementFormViewModel : ViewModelBase
{
    public MeasurementFormViewModel()
    {
        SaveCommand = ReactiveCommand.Create(() => SaveRequested?.Invoke());
        CancelCommand = ReactiveCommand.Create(() => CancelRequested?.Invoke());
        foreach (var option in MeasurementContextInfo.AllContexts) ContextOptions.Add(new ContextOption(option.Value, option.Label));
    }

    public event Action? SaveRequested;
    public event Action? CancelRequested;
    public event Action? Shown;

    public void NotifyShown() => Shown?.Invoke();

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    // true no mobile: os botões de ação ficam no cabeçalho (Não no rodapé), para o teclado não cobri-los.
    public bool IsMobileLayout { get; set; }

    public IReadOnlyList<string> MedicationOptions { get; } = new[] { "Não informado", "Antes da medicação", "Depois da medicação", "Não se aplica" };
    public ObservableCollection<ContextOption> ContextOptions { get; } = new();
    public IReadOnlyList<string> ArmOptions { get; } = new[] { "Não informado", "Direito", "Esquerdo" };
    public IReadOnlyList<string> PositionOptions { get; } = new[] { "Não informado", "Sentado", "Deitado", "Em pé" };

    public string Title => IsEditMode ? "Editar medição" : "Nova medição";

    private bool _isEditMode;
    public bool IsEditMode
    {
        get => _isEditMode;
        set { if (_isEditMode != value) { _isEditMode = value; this.RaisePropertyChanged(nameof(IsEditMode)); this.RaisePropertyChanged(nameof(Title)); } }
    }

    private string _bloodPressureInput = string.Empty;
    public string BloodPressureInput
    {
        get => _bloodPressureInput;
        set { if (_bloodPressureInput != value) { _bloodPressureInput = value; this.RaisePropertyChanged(nameof(BloodPressureInput)); MeasurementError = string.Empty; } }
    }

    private string _measurementError = string.Empty;
    public string MeasurementError { get => _measurementError; set => this.RaiseAndSetIfChanged(ref _measurementError, value); }

    private MedicationTiming _medicationTiming = MedicationTiming.NotInformed;
    public MedicationTiming MedicationTiming { get => _medicationTiming; set => this.RaiseAndSetIfChanged(ref _medicationTiming, value); }
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

    private DateTime? _measurementDate = DateTime.Today;
    public DateTime? MeasurementDate { get => _measurementDate; set => this.RaiseAndSetIfChanged(ref _measurementDate, value); }

    private TimeSpan? _measurementTime = DateTime.Now.TimeOfDay;
    public TimeSpan? MeasurementTime { get => _measurementTime; set => this.RaiseAndSetIfChanged(ref _measurementTime, value); }

    private string _notes = string.Empty;
    public string Notes { get => _notes; set => this.RaiseAndSetIfChanged(ref _notes, value); }

    private string _heartRateInput = string.Empty;
    public string HeartRateInput { get => _heartRateInput; set { if (_heartRateInput != value) { _heartRateInput = value; this.RaisePropertyChanged(nameof(HeartRateInput)); MeasurementError = string.Empty; } } }

    private bool _atRest;
    public bool AtRest { get => _atRest; set => this.RaiseAndSetIfChanged(ref _atRest, value); }

    private string _selectedArm = "Não informado";
    public string SelectedArm { get => _selectedArm; set => this.RaiseAndSetIfChanged(ref _selectedArm, value); }

    private string _selectedPosition = "Não informado";
    public string SelectedPosition { get => _selectedPosition; set => this.RaiseAndSetIfChanged(ref _selectedPosition, value); }

    public MeasurementContext SelectedContext()
    {
        var result = MeasurementContext.None;
        foreach (var option in ContextOptions)
            if (option.IsSelected) result |= option.Context;
        return result;
    }

    public void SetContext(MeasurementContext context)
    {
        foreach (var option in ContextOptions)
            option.IsSelected = (context & option.Context) != 0;
    }

    public Arm ParseArm() => SelectedArm switch
    {
        "Direito" => Arm.Right,
        "Esquerdo" => Arm.Left,
        _ => Arm.NotInformed
    };

    public BodyPosition ParsePosition() => SelectedPosition switch
    {
        "Sentado" => BodyPosition.Seated,
        "Deitado" => BodyPosition.Lying,
        "Em pé" => BodyPosition.Standing,
        _ => BodyPosition.NotInformed
    };
}
