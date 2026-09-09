using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using ReactiveUI;

namespace Pressio.ViewModels;

public sealed class PatientFormViewModel : ViewModelBase
{
    public PatientFormViewModel()
    {
        SaveCommand = ReactiveCommand.Create(() => SaveRequested?.Invoke());
        CancelCommand = ReactiveCommand.Create(() => CancelRequested?.Invoke());
        AddMedicationCommand = ReactiveCommand.Create(() =>
        {
            var t = NewMedicationText?.Trim();
            if (!string.IsNullOrWhiteSpace(t) && !Medications.Contains(t, StringComparer.OrdinalIgnoreCase)) Medications.Add(t);
            NewMedicationText = string.Empty;
        });
        RemoveMedicationCommand = ReactiveCommand.Create<string>(m => Medications.Remove(m));
    }

    public event Action? SaveRequested;
    public event Action? CancelRequested;
    public event Action? Shown;

    public void NotifyShown() => Shown?.Invoke();

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> AddMedicationCommand { get; }
    public ReactiveCommand<string, Unit> RemoveMedicationCommand { get; }

    // true no mobile: os botões de ação ficam no cabeçalho (Não no rodapé), para o teclado não cobri-los.
    public bool IsMobileLayout { get; set; }

    public string Title => IsEditMode ? "Editar usuário" : "Novo usuário";

    private bool _isEditMode;
    public bool IsEditMode
    {
        get => _isEditMode;
        set { if (_isEditMode != value) { _isEditMode = value; this.RaisePropertyChanged(nameof(IsEditMode)); this.RaisePropertyChanged(nameof(Title)); } }
    }

    private string _newPatientName = string.Empty;
    public string NewPatientName
    {
        get => _newPatientName;
        set { if (_newPatientName != value) { _newPatientName = value; this.RaisePropertyChanged(nameof(NewPatientName)); PatientError = string.Empty; } }
    }

    private string _patientError = string.Empty;
    public string PatientError { get => _patientError; set => this.RaiseAndSetIfChanged(ref _patientError, value); }

    private string _fullName = string.Empty;
    public string FullName { get => _fullName; set => this.RaiseAndSetIfChanged(ref _fullName, value); }

    private string _healthDetails = string.Empty;
    public string HealthDetails { get => _healthDetails; set => this.RaiseAndSetIfChanged(ref _healthDetails, value); }

    private string _doctorName = string.Empty;
    public string DoctorName { get => _doctorName; set => this.RaiseAndSetIfChanged(ref _doctorName, value); }

    public ObservableCollection<string> Medications { get; } = new();

    private string _newMedicationText = string.Empty;
    public string NewMedicationText { get => _newMedicationText; set => this.RaiseAndSetIfChanged(ref _newMedicationText, value); }

    public string MedicationsJoined => string.Join("; ", Medications);

    public void SetMedications(string? joined)
    {
        Medications.Clear();
        if (string.IsNullOrWhiteSpace(joined)) return;
        foreach (var m in joined.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (m.Length > 0) Medications.Add(m);
    }

    public void Reset()
    {
        NewPatientName = string.Empty;
        FullName = string.Empty;
        HealthDetails = string.Empty;
        DoctorName = string.Empty;
        Medications.Clear();
        NewMedicationText = string.Empty;
        PatientError = string.Empty;
    }
}
