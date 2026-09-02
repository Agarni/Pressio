using Android.App;
using Android.Content;
using Android.OS;
using Pressio.Services;

namespace Pressio.Android;

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class NotificationReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        var ctx = context!;
        var id = intent?.GetIntExtra("id", 0) ?? 0;
        var title = intent?.GetStringExtra("title") ?? "Pressio";
        var message = intent?.GetStringExtra("message") ?? "Hora de aferir a pressão";
        new AndroidNotificationService(ctx).Post(id, title, message);
    }
}
