using System;
using System.Reactive;
using ReactiveUI;

namespace Pressio.ViewModels;

public sealed class PatientFormViewModel : ViewModelBase
{
    public PatientFormViewModel()
    {
        SaveCommand = ReactiveCommand.Create(() => SaveRequested?.Invoke());
        CancelCommand = ReactiveCommand.Create(() => CancelRequested?.Invoke());
    }

    public event Action? SaveRequested;
    public event Action? CancelRequested;
    public event Action? Shown;

    public void NotifyShown() => Shown?.Invoke();

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

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
}
