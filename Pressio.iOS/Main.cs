using UIKit;
using Pressio.Services;

namespace Pressio.iOS;

public class Application
{
    // This is the main entry point of the application.
    static void Main(string[] args)
    {
        Notifications.Service = new IosNotificationService();
        FilePreview.Service = new IosFilePreviewService();
        // if you want to use a different Application Delegate class from "AppDelegate"
        // you can specify it here.
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
