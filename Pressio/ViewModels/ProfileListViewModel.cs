using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using Pressio.Models;
using ReactiveUI;

namespace Pressio.ViewModels;

public sealed class PatientProfileItem : ReactiveObject
{
    public PatientProfileItem(Patient patient, bool isActive) { Patient = patient; IsActive = isActive; }

    public Patient Patient { get; }
    public long Id => Patient.Id;
    public string Name => Patient.Name;

    private bool _isActive;
    public bool IsActive { get => _isActive; set => this.RaiseAndSetIfChanged(ref _isActive, value); }

    public string Initials
    {
        get
        {
            var parts = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return char.ToUpperInvariant(parts[0][0]).ToString();
            return "" + char.ToUpperInvariant(parts[0][0]) + char.ToUpperInvariant(parts[^1][0]);
        }
    }
}

public sealed class ProfileListViewModel : ViewModelBase
{
    public ProfileListViewModel()
    {
        AddCommand = ReactiveCommand.Create(() => AddRequested?.Invoke());
        EditCommand = ReactiveCommand.Create(() => EditRequested?.Invoke());
        DeleteCommand = ReactiveCommand.Create(() => DeleteRequested?.Invoke());
        ActivateCommand = ReactiveCommand.Create(() => ActivateRequested?.Invoke());
        BackCommand = ReactiveCommand.Create(() => BackRequested?.Invoke());
    }

    public ObservableCollection<PatientProfileItem> Profiles { get; } = new();

    private PatientProfileItem? _selected;
    public PatientProfileItem? SelectedProfile
    {
        get => _selected;
        set
        {
            this.RaiseAndSetIfChanged(ref _selected, value);
            this.RaisePropertyChanged(nameof(CanActivate));
        }
    }

    public bool CanActivate => SelectedProfile is { } p && !p.IsActive;

    public event Action? AddRequested;
    public event Action? EditRequested;
    public event Action? DeleteRequested;
    public event Action? ActivateRequested;
    public event Action? BackRequested;

    public ReactiveCommand<Unit, Unit> AddCommand { get; }
    public ReactiveCommand<Unit, Unit> EditCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> ActivateCommand { get; }
    public ReactiveCommand<Unit, Unit> BackCommand { get; }
}
