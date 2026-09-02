using System;
using System.Collections.Generic;
using System.Linq;

namespace Pressio.Models;

[Flags]
public enum ReminderDays
{
    None = 0,
    Sunday = 1 << 0,
    Monday = 1 << 1,
    Tuesday = 1 << 2,
    Wednesday = 1 << 3,
    Thursday = 1 << 4,
    Friday = 1 << 5,
    Saturday = 1 << 6,
    All = Sunday | Monday | Tuesday | Wednesday | Thursday | Friday | Saturday
}

public sealed record Reminder(long Id, TimeSpan Time, ReminderDays Days, bool Enabled, string? Note = null)
{
    public string DisplayTime => Time.ToString(@"hh\:mm");
    public bool IsEveryDay => Days == ReminderDays.All;
    public string DisplayDays => ReminderInfo.DescribeDays(Days);
}

public static class ReminderInfo
{
    public static IReadOnlyList<(ReminderDays Value, string Label)> AllDays { get; } = new[]
    {
        (ReminderDays.Sunday, "Dom"),
        (ReminderDays.Monday, "Seg"),
        (ReminderDays.Tuesday, "Ter"),
        (ReminderDays.Wednesday, "Qua"),
        (ReminderDays.Thursday, "Qui"),
        (ReminderDays.Friday, "Sex"),
        (ReminderDays.Saturday, "Sáb"),
    };

    public static string DescribeDays(ReminderDays days)
    {
        var selected = AllDays.Where(x => (days & x.Value) != 0).Select(x => x.Label);
        var text = string.Join(", ", selected);
        return text.Length == 0 ? "Todos os dias" : text;
    }
}
