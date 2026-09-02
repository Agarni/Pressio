using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Foundation;
using UserNotifications;
using Pressio.Models;
using Pressio.Services;

namespace Pressio.iOS;

public sealed class IosNotificationService : INotificationService
{
    private static bool _initialized;

    public Task ScheduleAsync(Reminder reminder)
    {
        EnsureInitialized();
        var content = BuildContent(reminder);
        foreach (var weekday in Weekdays(reminder.Days))
        {
            var components = new NSDateComponents { Hour = reminder.Time.Hours, Minute = reminder.Time.Minutes };
            if (weekday.HasValue) components.Weekday = weekday.Value;
            var trigger = UNCalendarNotificationTrigger.CreateTrigger(components, true);
            var request = UNNotificationRequest.FromIdentifier($"{reminder.Id}-{weekday ?? 0}", content, trigger);
            UNUserNotificationCenter.Current.AddNotificationRequest(request, null);
        }
        return Task.CompletedTask;
    }

    public Task CancelAsync(long reminderId)
    {
        var ids = new List<string>();
        for (var d = 0; d <= 7; d++) ids.Add($"{reminderId}-{d}");
        UNUserNotificationCenter.Current.RemovePendingNotificationRequests(ids.ToArray());
        return Task.CompletedTask;
    }

    public Task ShowNowAsync(string title, string message)
    {
        EnsureInitialized();
        var content = new UNMutableNotificationContent { Title = title, Body = message, Sound = UNNotificationSound.Default };
        var request = UNNotificationRequest.FromIdentifier(Guid.NewGuid().ToString(), content, UNTimeIntervalNotificationTrigger.CreateTrigger(1, false));
        UNUserNotificationCenter.Current.AddNotificationRequest(request, null);
        return Task.CompletedTask;
    }

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        UNUserNotificationCenter.Current.RequestAuthorization(UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound | UNAuthorizationOptions.Badge, (granted, error) => { });
        UNUserNotificationCenter.Current.Delegate = new PressioNotificationDelegate();
    }

    private static UNMutableNotificationContent BuildContent(Reminder reminder)
    {
        var body = reminder.DisplayTime + " — hora de aferir a pressão";
        if (!string.IsNullOrWhiteSpace(reminder.Note)) body += "\n" + reminder.Note;
        return new UNMutableNotificationContent { Title = "Pressio", Body = body, Sound = UNNotificationSound.Default };
    }

    private static IEnumerable<int?> Weekdays(ReminderDays days)
    {
        if (days == ReminderDays.All || days == ReminderDays.None)
        {
            yield return null;
            yield break;
        }
        foreach (var (flag, weekday) in DayMap)
            if ((days & flag) != 0) yield return weekday;
    }

    private static readonly (ReminderDays Flag, int Weekday)[] DayMap =
    {
        (ReminderDays.Sunday, 1), (ReminderDays.Monday, 2), (ReminderDays.Tuesday, 3),
        (ReminderDays.Wednesday, 4), (ReminderDays.Thursday, 5), (ReminderDays.Friday, 6), (ReminderDays.Saturday, 7)
    };
}

public sealed class PressioNotificationDelegate : UNUserNotificationCenterDelegate
{
    public override void WillPresentNotification(UNUserNotificationCenter center, UNNotification notification, Action<UNNotificationPresentationOptions> completionHandler)
        => completionHandler(UNNotificationPresentationOptions.Alert | UNNotificationPresentationOptions.Sound);
}
