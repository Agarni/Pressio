using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Pressio.Services;
using ReactiveUI.Avalonia;

namespace Pressio.Android
{
    [Application]
    public class Application : AvaloniaAndroidApplication<App>
    {
        protected Application(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            Notifications.Service = new AndroidNotificationService(this);
            return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .UseReactiveUI();
        }
    }
}
