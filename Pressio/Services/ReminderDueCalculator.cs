using System;
using Pressio.Models;

namespace Pressio.Services;

// Decide quando um lembrete deve disparar (dia da semana + janela de 1 min no horário).
// Extraído do MainViewModel para ser testável isoladamente.
public static class ReminderDueCalculator
{
    public static bool IsDue(bool enabled, ReminderDays days, TimeSpan time, DateTime now)
        => enabled
           && days != ReminderDays.None
           && (days & DayFlag(now.DayOfWeek)) != 0
           && Math.Abs((time - now.TimeOfDay).TotalMinutes) < 1;

    public static ReminderDays DayFlag(DayOfWeek day) => day switch
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
}
