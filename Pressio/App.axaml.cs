using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Pressio.ViewModels;
using Pressio.Views;

namespace Pressio;

public partial class App : Application
{
    public App()
    {
        Name = "Pressio";
    }

    public static void ApplyAppearance(string appearance, string primaryColor)
    {
        if (Current is not App app) return;
        app.RequestedThemeVariant = appearance == "Escuro" ? ThemeVariant.Dark : ThemeVariant.Light;
        var (baseColor, hover, pressed) = primaryColor switch
        {
            "Azul" => ("#2775D8", "#4089E6", "#1B5EB2"),
            "Verde" => ("#16846F", "#239983", "#106A59"),
            "Roxo" => ("#7954C8", "#8D6ED7", "#5F3DA8"),
            "Coral" => ("#D65B54", "#E5726B", "#AF413C"),
            _ => ("#5B5BD6", "#6D6DE0", "#4848B3")
        };
        app.Resources["PressioPrimaryBrush"] = new SolidColorBrush(Color.Parse(baseColor));
        app.Resources["PressioPrimaryHoverBrush"] = new SolidColorBrush(Color.Parse(hover));
        app.Resources["PressioPrimaryPressedBrush"] = new SolidColorBrush(Color.Parse(pressed));
        var dark = appearance == "Escuro";
        app.Resources["PressioBackgroundBrush"] = new SolidColorBrush(Color.Parse(dark ? "#171827" : "#F7F7FC"));
        app.Resources["PressioSurfaceBrush"] = new SolidColorBrush(Color.Parse(dark ? "#222438" : "#FFFFFF"));
        app.Resources["PressioTextBrush"] = new SolidColorBrush(Color.Parse(dark ? "#F2F3FF" : "#242B4A"));
        app.Resources["PressioMutedBrush"] = new SolidColorBrush(Color.Parse(dark ? "#B7BAD2" : "#73799B"));
        app.Resources["PressioBorderBrush"] = new SolidColorBrush(Color.Parse(dark ? "#3A3D57" : "#E0E3F1"));
        app.Resources["PressioBannerBrush"] = new SolidColorBrush(Color.Parse(dark ? "#0F1020" : "#242B4A"));
        app.Resources["PressioBannerTextBrush"] = new SolidColorBrush(Color.Parse(dark ? "#D6D8F7" : "#D6D8F7"));
    }
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
        {
            singleViewFactoryApplicationLifetime.MainViewFactory = () => new MainView { DataContext = new MainViewModel(isMobileLayout: true) };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel(isMobileLayout: true)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
