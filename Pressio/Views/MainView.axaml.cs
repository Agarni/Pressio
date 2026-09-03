using System;
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

        vm.OpenFileInteraction.RegisterHandler(async ctx =>
        {
            var provider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (provider is null) { ctx.SetOutput(null); return; }
            var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Selecione o arquivo de backup (.db)",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Backup") { Patterns = new[] { "*.db" } } }
            });
            ctx.SetOutput(files.Count > 0 ? files[0].TryGetLocalPath() : null);
        });

        vm.FolderPickerInteraction.RegisterHandler(async ctx =>
        {
            var provider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (provider is null) { ctx.SetOutput(null); return; }
            try
            {
                var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Escolha a pasta de sincronização (OneDrive / Google Drive / iCloud)",
                    AllowMultiple = false
                });
                // Para pastas "cloud" (ex.: iCloud), TryGetLocalPath pode retornar null;
                // usamos o local path da URI como fallback.
                var path = folders.Count > 0
                    ? folders[0].TryGetLocalPath() ?? folders[0].Path.LocalPath
                    : null;
                ctx.SetOutput(path);
            }
            catch
            {
                ctx.SetOutput(null);
            }
        });
    }
}
