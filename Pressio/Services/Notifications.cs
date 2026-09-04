using System.Threading.Tasks;
using Pressio.Models;

namespace Pressio.Services;

public interface INotificationService
{
    Task ScheduleAsync(Reminder reminder);
    Task CancelAsync(long reminderId);
    Task ShowNowAsync(string title, string message);
    // true quando o SO agenda notificações de verdade (iOS/Android). Quando true,
    // o fallback in-app (ShowNowAsync) é desativado para não duplicar o lembrete.
    bool SupportsScheduledNotifications { get; }
}

public static class Notifications
{
    public static INotificationService Service { get; set; } = new NullNotificationService();

    private sealed class NullNotificationService : INotificationService
    {
        public Task ScheduleAsync(Reminder reminder) => Task.CompletedTask;
        public Task CancelAsync(long reminderId) => Task.CompletedTask;
        public Task ShowNowAsync(string title, string message) => Task.CompletedTask;
        public bool SupportsScheduledNotifications => false;
    }
}
