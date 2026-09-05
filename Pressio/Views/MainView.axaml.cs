using System;
using System.IO;
using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Pressio.Services;
using Pressio.ViewModels;

namespace Pressio.Views;

public partial class MainView : UserControl
{
    private bool _interactionsRegistered;
    private IStorageFolder? _syncFolder;
    private IStorageFile? _lastExportFile;

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // No mobile a raiz é edge-to-edge (AutoSafeAreaPadding=False); aplicamos o inset
        // do sistema como padding para o conteúdo ficar abaixo da barra de status/notch,
        // enquanto o fundo (Root) ocupa a tela inteira.
        Dispatcher.UIThread.Post(ApplySafeAreaPadding, DispatcherPriority.Loaded);
    }

    private void ApplySafeAreaPadding()
    {
        var top = TopLevel.GetTopLevel(this)?.InsetsManager?.SafeAreaPadding.Top ?? 0;
        Root.Padding = new Thickness(0, top, 0, 0);
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
            _lastExportFile = file;
            ctx.SetOutput(file?.TryGetLocalPath());
        });

        vm.OpenExportInteraction.RegisterHandler(async ctx =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            var launcher = topLevel?.Launcher;
            try
            {
                // Primeiro tenta o launcher nativo (desktop/Android); no iOS ele não abre
                // arquivos, então cai no preview nativo (QLPreviewController).
                var item = _lastExportFile;
                if (item is null && topLevel?.StorageProvider is { } provider && !string.IsNullOrWhiteSpace(ctx.Input))
                    item = await provider.TryGetFileFromPathAsync(ctx.Input);
                if (item is not null && launcher is not null)
                {
                    if (await launcher.LaunchFileAsync(item)) { ctx.SetOutput(true); return; }
                    if (await launcher.LaunchUriAsync(item.Path)) { ctx.SetOutput(true); return; }
                }

                var path = item?.TryGetLocalPath() ?? item?.Path?.LocalPath ?? ctx.Input;
                ctx.SetOutput(await FilePreview.Service.PreviewAsync(path));
            }
            catch
            {
                try { ctx.SetOutput(await FilePreview.Service.PreviewAsync(ctx.Input)); }
                catch { ctx.SetOutput(false); }
            }
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
                _syncFolder = folders.Count > 0 ? folders[0] : null;
                // Para pastas "cloud" (ex.: iCloud), TryGetLocalPath pode retornar null;
                // usamos o local path da URI como fallback.
                var path = _syncFolder is not null
                    ? _syncFolder.TryGetLocalPath() ?? _syncFolder.Path.LocalPath
                    : null;
                ctx.SetOutput(path);
            }
            catch
            {
                _syncFolder = null;
                ctx.SetOutput(null);
            }
        });

        vm.SyncNowInteraction.RegisterHandler(async ctx =>
        {
            var provider = TopLevel.GetTopLevel(this)?.StorageProvider;
            var folder = _syncFolder;
            // Recomposição no desktop (pasta persistida entre aberturas).
            if (folder is null && !string.IsNullOrWhiteSpace(vm.Settings.SyncDirectory) && provider is not null)
                folder = await provider.TryGetFolderFromPathAsync(vm.Settings.SyncDirectory);
            if (folder is null) { vm.SetSyncError("Escolha primeiro a pasta de sincronização."); ctx.SetOutput(Unit.Default); return; }

            try
            {
                var file = await folder.GetFileAsync("pressio-sync.json");
                string? remoteJson = null;
                if (file is not null)
                {
                    await using var read = await file.OpenReadAsync();
                    using var reader = new StreamReader(read);
                    remoteJson = await reader.ReadToEndAsync();
                }

                var mergedJson = vm.ApplyRemoteSync(remoteJson);

                // Reescreve o arquivo (vazio primeiro para sobrescrever por completo).
                var outFile = file ?? await folder.CreateFileAsync("pressio-sync.json");
                await using var write = await outFile!.OpenWriteAsync();
                write.SetLength(0);
                using var writer = new StreamWriter(write);
                await writer.WriteAsync(mergedJson);
            }
            catch (Exception ex)
            {
                vm.SetSyncError("Falha ao sincronizar: " + ex.Message);
            }
            ctx.SetOutput(Unit.Default);
        });
    }
}
