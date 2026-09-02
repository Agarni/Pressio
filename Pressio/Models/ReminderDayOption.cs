using ReactiveUI;

namespace Pressio.Models;

public sealed class ReminderDayOption : ReactiveObject
{
    public ReminderDayOption(ReminderDays value, string label)
    {
        Value = value;
        Label = label;
    }

    public ReminderDays Value { get; }
    public string Label { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }
}
