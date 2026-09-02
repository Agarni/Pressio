using System;
using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Pressio.ViewModels;

namespace Pressio.Views;

public partial class MainView : UserControl
{
    private bool _interactionsRegistered;

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_interactionsRegistered || DataContext is not MainViewModel vm) return;
        _interactionsRegistered = true;

        vm.ExportFileInteraction.RegisterHandler(async ctx =>
        {
            var provider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (provider is null) { ctx.SetOutput(null); return; }

            IStorageFolder? start = null;
            if (!string.IsNullOrWhiteSpace(ctx.Input.StartDirectory))
                start = await provider.TryGetFolderFromPathAsync(ctx.Input.StartDirectory);

            var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = ctx.Input.FileName,
                DefaultExtension = ctx.Input.Extension,
                SuggestedStartLocation = start,
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(ctx.Input.Kind) { Patterns = new[] { $"*{ctx.Input.Extension}" } }
                }
            });
            ctx.SetOutput(file?.TryGetLocalPath());
        });

        vm.ConfirmOpenInteraction.RegisterHandler(async ctx =>
        {
            if (TopLevel.GetTopLevel(this) is not Window owner) { ctx.SetOutput(false); return; }

            var panel = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
            panel.Children.Add(new TextBlock { Text = ctx.Input, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold });
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
            var no = new Button { Content = "Agora não", Classes = { "secondary-button" } };
            var yes = new Button { Content = "Abrir PDF", Classes = { "primary-button" } };
            buttons.Children.Add(no);
            buttons.Children.Add(yes);
            panel.Children.Add(buttons);

            var dialog = new Window { Width = 380, Height = 175, CanResize = false, Title = "Pressio", Content = panel };
            yes.Click += (_, _) => dialog.Close(true);
            no.Click += (_, _) => dialog.Close(false);

            ctx.SetOutput(await dialog.ShowDialog<bool>(owner));
        });

        vm.ReminderBannerInteraction.RegisterHandler(async ctx =>
        {
            if (TopLevel.GetTopLevel(this) is Window owner)
            {
                var message = ctx.Input.DisplayTime + " — hora de aferir a pressão";
                if (!string.IsNullOrWhiteSpace(ctx.Input.Note)) message += "\n" + ctx.Input.Note;

                var panel = new StackPanel { Margin = new Thickness(20), Spacing = 14 };
                panel.Children.Add(new TextBlock { Text = "Lembrete de aferição", FontWeight = FontWeight.SemiBold, FontSize = 16 });
                panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
                var ok = new Button { Content = "OK", Classes = { "primary-button" }, HorizontalAlignment = HorizontalAlignment.Right };
                panel.Children.Add(ok);

                var dialog = new Window { Width = 340, Height = 190, CanResize = false, Title = "Pressio", Content = panel };
                ok.Click += (_, _) => dialog.Close(true);
                await dialog.ShowDialog<bool>(owner);
            }
            ctx.SetOutput(Unit.Default);
        });
    }
}
