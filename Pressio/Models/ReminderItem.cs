using System;
using ReactiveUI;

namespace Pressio.Models;

public sealed class ReminderItem : ReactiveObject
{
    private readonly Action<ReminderItem>? _onEnabledChanged;

    public ReminderItem(Reminder reminder, Action<ReminderItem>? onEnabledChanged = null)
    {
        Id = reminder.Id;
        Time = reminder.Time;
        Days = reminder.Days;
        Note = reminder.Note;
        _enabled = reminder.Enabled;
        _onEnabledChanged = onEnabledChanged;
    }

    public long Id { get; }
    public TimeSpan Time { get; }
    public ReminderDays Days { get; }
    public string? Note { get; }
    public string DisplayTime => Time.ToString(@"hh\:mm");
    public string DisplayDays => ReminderInfo.DescribeDays(Days);

    private bool _enabled;
    public bool Enabled
    {
        get => _enabled;
        set { if (this.RaiseAndSetIfChanged(ref _enabled, value)) _onEnabledChanged?.Invoke(this); }
    }
}
