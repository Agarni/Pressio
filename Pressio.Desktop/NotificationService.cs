using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Pressio.Models;
using Pressio.Services;

namespace Pressio.Desktop;

public sealed class DesktopNotificationService : INotificationService
{
    public Task ScheduleAsync(Reminder reminder) => Task.CompletedTask;
    public Task CancelAsync(long reminderId) => Task.CompletedTask;
    public bool SupportsScheduledNotifications => false;

    public Task ShowNowAsync(string title, string message)
    {
        TryShow(title, message);
        return Task.CompletedTask;
    }

    private static void TryShow(string title, string message)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                Run("osascript", "-e", $"display notification \"{Escape(message)}\" with title \"{Escape(title)}\"");
            }
            else if (OperatingSystem.IsLinux())
            {
                Run("notify-send", title, message);
            }
            else if (OperatingSystem.IsWindows())
            {
                Run("msg", "*", $"{title}: {message}");
            }
        }
        catch
        {
            // silencioso: exibição in-app continua funcionando
        }
    }

    private static void Run(string fileName, params string[] args)
    {
        var psi = new ProcessStartInfo(fileName) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using (Process.Start(psi)) { }
    }

    private static string Escape(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\r\n", "\\n")
        .Replace("\n", "\\n");
}
