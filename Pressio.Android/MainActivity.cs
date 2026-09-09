using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Activity;
using Avalonia;
using Avalonia.Android;
using Pressio;
using ReactiveUI.Avalonia;

namespace Pressio.Android;

[Activity(
    Label = "Pressio",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    private const int HealthPermissionRequest = 4357;
    private const int NotificationPermissionRequest = 4358;
    private static TaskCompletionSource<bool>? _healthPermTcs;
    private static MainActivity? _current;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _current = this;
        OnBackPressedDispatcher.AddCallback(this, new BackCallback(OnBackPressedDispatcher));
        // Android 13+ exige permissão de notificação em runtime; sem ela a notificação é bloqueada.
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu &&
            CheckSelfPermission("android.permission.POST_NOTIFICATIONS") != Permission.Granted)
        {
            RequestPermissions(new[] { "android.permission.POST_NOTIFICATIONS" }, NotificationPermissionRequest);
        }
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
    }

    protected override void OnStop()
    {
        base.OnStop();
        // Ao entrar em segundo plano, envia as alterações recentes para a nuvem.
        _ = App.Main?.SyncNowFromHost();
    }

    public static Task<bool> RequestHealthPermissionAsync()
    {
        var activity = _current;
        if (activity is null) return Task.FromResult(false);
        var tcs = new TaskCompletionSource<bool>();
        _healthPermTcs = tcs;
        var intent = new Intent();
        intent.SetClassName(activity, "androidx.health.connect.client.PermissionActivity");
        intent.PutStringArrayListExtra("androidx.health.connect.client.extra.REQUESTED_PERMISSIONS", new List<string>
        {
            "android.permission.health.READ_BLOOD_PRESSURE",
            "android.permission.health.WRITE_BLOOD_PRESSURE",
            "android.permission.health.READ_HEART_RATE",
            "android.permission.health.WRITE_HEART_RATE",
        });
        activity.StartActivityForResult(intent, HealthPermissionRequest);
        return tcs.Task;
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == HealthPermissionRequest)
        {
            _healthPermTcs?.TrySetResult(resultCode == Result.Ok);
            _healthPermTcs = null;
        }
    }

    private sealed class BackCallback : OnBackPressedCallback
    {
        private readonly OnBackPressedDispatcher _dispatcher;

        public BackCallback(OnBackPressedDispatcher dispatcher) : base(true) => _dispatcher = dispatcher;

        public override void HandleOnBackPressed()
        {
            if (App.Main?.HandleBack() == true)
                return;
            // Não consumiu: deixa o padrão agir (fecha a activity) e volta a interceptar depois.
            Enabled = false;
            _dispatcher.OnBackPressed();
            Enabled = true;
        }
    }
}
