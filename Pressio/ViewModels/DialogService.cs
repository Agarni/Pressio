using System;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;

namespace Pressio.ViewModels;

// Serviço de diálogo em-app (funciona no mobile onde Window.ShowDialog não existe).
// Usado para confirmações (Sim/Não) e avisos (OK) de forma reutilizável.
public sealed class DialogService : ViewModelBase
{
    public DialogService()
    {
        OkCommand = ReactiveCommand.Create(() => Resolve(true));
        CancelCommand = ReactiveCommand.Create(() => Resolve(false));
    }

    public ReactiveCommand<Unit, Unit> OkCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    private bool _isVisible;
    public bool IsVisible { get => _isVisible; set { if (this.RaiseAndSetIfChanged(ref _isVisible, value)) this.RaisePropertyChanged(nameof(IsCancelVisible)); } }
    public bool IsCancelVisible => IsVisible && _confirming;

    private bool _confirming;
    private string _title = string.Empty;
    public string Title { get => _title; set => this.RaiseAndSetIfChanged(ref _title, value); }
    private string _message = string.Empty;
    public string Message { get => _message; set => this.RaiseAndSetIfChanged(ref _message, value); }
    private string _confirmText = "OK";
    public string ConfirmText { get => _confirmText; set => this.RaiseAndSetIfChanged(ref _confirmText, value); }
    private string _cancelText = "Cancelar";
    public string CancelText { get => _cancelText; set => this.RaiseAndSetIfChanged(ref _cancelText, value); }

    private TaskCompletionSource<bool>? _tcs;

    public Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancelar")
    {
        _confirming = true;
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        CancelText = cancelText;
        _tcs = new TaskCompletionSource<bool>();
        IsVisible = true;
        return _tcs.Task;
    }

    public Task ShowInfoAsync(string title, string message, string confirmText = "OK")
    {
        _confirming = false;
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        _tcs = new TaskCompletionSource<bool>();
        IsVisible = true;
        return _tcs.Task;
    }

    private void Resolve(bool result)
    {
        IsVisible = false;
        _tcs?.TrySetResult(result);
        _tcs = null;
    }
}
