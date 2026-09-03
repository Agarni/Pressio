using System;
using System.Collections.ObjectModel;
using System.Reactive;
using Pressio.Models;
using ReactiveUI;

namespace Pressio.ViewModels;

public sealed class ReminderFormViewModel : ViewModelBase
{
    public ReminderFormViewModel()
    {
        SaveCommand = ReactiveCommand.Create(() => SaveRequested?.Invoke());
        CancelCommand = ReactiveCommand.Create(() => CancelRequested?.Invoke());
        foreach (var day in ReminderInfo.AllDays) ReminderDayOptions.Add(new ReminderDayOption(day.Value, day.Label));
    }

    public event Action? SaveRequested;
    public event Action? CancelRequested;

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public string Title => IsEditMode ? "Editar lembrete" : "Novo lembrete";

    private bool _isEditMode;
    public bool IsEditMode
    {
        get => _isEditMode;
        set { if (_isEditMode != value) { _isEditMode = value; this.RaisePropertyChanged(nameof(IsEditMode)); this.RaisePropertyChanged(nameof(Title)); } }
    }

    private TimeSpan? _reminderTime;
    public TimeSpan? ReminderTime { get => _reminderTime; set => this.RaiseAndSetIfChanged(ref _reminderTime, value); }

    private bool _reminderEnabled = true;
    public bool ReminderEnabled { get => _reminderEnabled; set => this.RaiseAndSetIfChanged(ref _reminderEnabled, value); }

    private string _reminderNote = string.Empty;
    public string ReminderNote { get => _reminderNote; set => this.RaiseAndSetIfChanged(ref _reminderNote, value); }

    public ObservableCollection<ReminderDayOption> ReminderDayOptions { get; } = new();

    public ReminderDays SelectedDays()
    {
        var result = ReminderDays.None;
        foreach (var option in ReminderDayOptions)
            if (option.IsSelected) result |= option.Value;
        return result;
    }

    public void SetDays(ReminderDays days)
    {
        foreach (var option in ReminderDayOptions)
            option.IsSelected = (days & option.Value) != 0;
    }
}
