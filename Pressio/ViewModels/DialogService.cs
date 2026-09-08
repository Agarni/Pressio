using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;

namespace Pressio.ViewModels;

// Opção de um menu/ação em folha (usado pelo ShowOptionsAsync).
public sealed class DialogOption
{
    public DialogOption(string label, ICommand command) { Label = label; Command = command; }
    public string Label { get; }
    public ICommand Command { get; }
}

// Serviço de diálogo em-app (funciona no mobile onde Window.ShowDialog não existe).
// Confirmações (Sim/Não), avisos (OK) e uma "folha" com lista de ações (ShowOptionsAsync).
public sealed class DialogService : ViewModelBase
{
    public DialogService()
    {
        OkCommand = ReactiveCommand.Create(() => ResolveBool(true));
        CancelCommand = ReactiveCommand.Create(Cancel);
    }

    public ReactiveCommand<Unit, Unit> OkCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    private bool _isVisible;
    public bool IsVisible { get => _isVisible; set { if (this.RaiseAndSetIfChanged(ref _isVisible, value)) this.RaisePropertyChanged(nameof(IsCancelVisible)); } }
    public bool IsCancelVisible => IsVisible && (_confirming || _optionsMode);

    private bool _confirming;
    private bool _optionsMode;
    public bool IsOptionsMode { get => _optionsMode; private set { if (this.RaiseAndSetIfChanged(ref _optionsMode, value)) this.RaisePropertyChanged(nameof(IsCancelVisible)); } }

    private string _title = string.Empty;
    public string Title { get => _title; set => this.RaiseAndSetIfChanged(ref _title, value); }
    private string _message = string.Empty;
    public string Message { get => _message; set => this.RaiseAndSetIfChanged(ref _message, value); }
    private string _confirmText = "OK";
    public string ConfirmText { get => _confirmText; set => this.RaiseAndSetIfChanged(ref _confirmText, value); }
    private string _cancelText = "Cancelar";
    public string CancelText { get => _cancelText; set => this.RaiseAndSetIfChanged(ref _cancelText, value); }

    public ObservableCollection<DialogOption> Options { get; } = new();

    private TaskCompletionSource<bool>? _tcsBool;
    private TaskCompletionSource<string?>? _tcsOptions;

    public Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancelar")
    {
        IsOptionsMode = false;
        _confirming = true;
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        CancelText = cancelText;
        _tcsBool = new TaskCompletionSource<bool>();
        IsVisible = true;
        return _tcsBool.Task;
    }

    public Task ShowInfoAsync(string title, string message, string confirmText = "OK")
    {
        IsOptionsMode = false;
        _confirming = false;
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        _tcsBool = new TaskCompletionSource<bool>();
        IsVisible = true;
        return _tcsBool.Task;
    }

    // Folha com uma lista de ações; retorna o rótulo escolhido ou null se fechou.
    public Task<string?> ShowOptionsAsync(string title, System.Collections.Generic.IReadOnlyList<string> options, string cancelText = "Fechar")
    {
        IsOptionsMode = true;
        _confirming = false;
        Title = title;
        Message = string.Empty;
        CancelText = cancelText;
        Options.Clear();
        foreach (var label in options)
        {
            var captured = label;
            Options.Add(new DialogOption(label, ReactiveCommand.Create(() => ResolveOptions(captured))));
        }
        _tcsOptions = new TaskCompletionSource<string?>();
        IsVisible = true;
        return _tcsOptions.Task;
    }

    private void Cancel()
    {
        if (_optionsMode) ResolveOptions(null);
        else ResolveBool(false);
    }

    private void ResolveBool(bool result)
    {
        IsVisible = false;
        _tcsBool?.TrySetResult(result);
        _tcsBool = null;
    }

    private void ResolveOptions(string? label)
    {
        IsVisible = false;
        _tcsOptions?.TrySetResult(label);
        _tcsOptions = null;
    }
}
