using System;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Pressio.Models;
using Pressio.Services;

namespace Pressio.Android;

public sealed class AndroidNotificationService : INotificationService
{
    private const string ChannelId = "pressio_reminders";
    private readonly Context _context;
    private readonly NotificationManager _manager;

    public AndroidNotificationService(Context context)
    {
        _context = context;
        _manager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
        EnsureChannel();
    }

    public Task ScheduleAsync(Reminder reminder)
    {
        var alarm = (AlarmManager)_context.GetSystemService(Context.AlarmService)!;
        var trigger = NextTrigger(reminder).ToUnixTimeMilliseconds();
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            alarm.SetAndAllowWhileIdle(AlarmType.RtcWakeup, trigger, BuildPendingIntent(reminder));
        else
            alarm.Set(AlarmType.RtcWakeup, trigger, BuildPendingIntent(reminder));
        return Task.CompletedTask;
    }

    public Task CancelAsync(long reminderId)
    {
        var alarm = (AlarmManager)_context.GetSystemService(Context.AlarmService)!;
        alarm.Cancel(BuildPendingIntent(new Reminder(reminderId, default, ReminderDays.None, false)));
        _manager.Cancel((int)reminderId);
        return Task.CompletedTask;
    }

    public Task ShowNowAsync(string title, string message)
    {
        Post((int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % int.MaxValue), title, message);
        return Task.CompletedTask;
    }

    internal void Post(int id, string title, string message)
    {
        var builder = Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? new Notification.Builder(_context, ChannelId)
            : new Notification.Builder(_context);
        builder.SetContentTitle(title)
            .SetContentText(message)
            .SetSmallIcon(Resource.Drawable.Icon)
            .SetAutoCancel(true);
        _manager.Notify(id, builder.Build());
    }

    private PendingIntent BuildPendingIntent(Reminder reminder)
    {
        var intent = new Intent(_context, typeof(NotificationReceiver));
        intent.PutExtra("id", (int)reminder.Id);
        intent.PutExtra("title", "Pressio — hora de aferir a pressão");
        intent.PutExtra("message", reminder.DisplayTime + " — hora de aferir a pressão");
        return PendingIntent.GetBroadcast(_context, (int)reminder.Id, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
    }

    private void EnsureChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, "Lembretes de aferição", NotificationImportance.Default)
            {
                Description = "Lembretes para aferir a pressão"
            };
            _manager.CreateNotificationChannel(channel);
        }
    }

    private static DateTimeOffset NextTrigger(Reminder reminder)
    {
        var now = DateTime.Now;
        for (var d = 0; d < 8; d++)
        {
            var day = now.Date.AddDays(d).Add(reminder.Time);
            if (day > now && MatchesDay(reminder.Days, day.DayOfWeek)) return new DateTimeOffset(day);
        }
        return new DateTimeOffset(now.AddMinutes(1));
    }

    private static bool MatchesDay(ReminderDays days, DayOfWeek day)
    {
        if (days == ReminderDays.All || days == ReminderDays.None) return true;
        var flag = day switch
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
        return (days & flag) != 0;
    }
}
