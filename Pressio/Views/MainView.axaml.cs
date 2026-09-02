using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Pressio.ViewModels;

namespace Pressio.Views;

public partial class MainView : UserControl
{
    private bool _exportInteractionRegistered;

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_exportInteractionRegistered || DataContext is not MainViewModel vm) return;
        _exportInteractionRegistered = true;

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
    }
}
