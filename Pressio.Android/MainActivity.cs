using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Activity;
using Avalonia;
using Avalonia.Android;
using Pressio;
using ReactiveUI.Avalonia;

namespace Pressio.Android;

[Activity(
    Label = "Pressio.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        OnBackPressedDispatcher.AddCallback(this, new BackCallback(OnBackPressedDispatcher));
    }

    // Fecha a tela/popup atual com o botão voltar; se não houver o que fechar, segue o padrão (sai do app).
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
