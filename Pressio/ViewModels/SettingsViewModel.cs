using System;
using System.Collections.Generic;
using System.Reactive;
using ReactiveUI;

namespace Pressio.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    public SettingsViewModel()
    {
        ApplyCommand = ReactiveCommand.Create(() => ApplyRequested?.Invoke());
        CancelCommand = ReactiveCommand.Create(() => CancelRequested?.Invoke());
        BackupCommand = ReactiveCommand.Create(() => BackupRequested?.Invoke());
        RestoreCommand = ReactiveCommand.Create(() => RestoreRequested?.Invoke());
        SelectPrimaryColorCommand = ReactiveCommand.Create<string>(color => SelectedPrimaryColor = color);
        ChooseDirectoryCommand = ReactiveCommand.Create(() => ChooseDirectoryRequested?.Invoke());
        SaveSupabaseCommand = ReactiveCommand.Create(() => SaveSupabaseRequested?.Invoke());
        SignInCommand = ReactiveCommand.Create(() => SignInRequested?.Invoke());
        SignUpCommand = ReactiveCommand.Create(() => SignUpRequested?.Invoke());
        SignOutCommand = ReactiveCommand.Create(() => SignOutRequested?.Invoke());
        SyncNowCommand = ReactiveCommand.Create(() => SyncRequested?.Invoke());
        SendMagicLinkCommand = ReactiveCommand.Create(() => SendMagicLinkRequested?.Invoke());
    }

    public event Action? ApplyRequested;
    public event Action? CancelRequested;
    public event Action? BackupRequested;
    public event Action? RestoreRequested;
    public event Action? ChooseDirectoryRequested;
    public event Action? SaveSupabaseRequested;
    public event Action? SignInRequested;
    public event Action? SignUpRequested;
    public event Action? SignOutRequested;
    public event Action? SyncRequested;
    public event Action? SendMagicLinkRequested;

    public ReactiveCommand<Unit, Unit> ApplyCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> BackupCommand { get; }
    public ReactiveCommand<Unit, Unit> RestoreCommand { get; }
    public ReactiveCommand<string, Unit> SelectPrimaryColorCommand { get; }
    public ReactiveCommand<Unit, Unit> ChooseDirectoryCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveSupabaseCommand { get; }
    public ReactiveCommand<Unit, Unit> SignInCommand { get; }
    public ReactiveCommand<Unit, Unit> SignUpCommand { get; }
    public ReactiveCommand<Unit, Unit> SignOutCommand { get; }
    public ReactiveCommand<Unit, Unit> SyncNowCommand { get; }
    public ReactiveCommand<Unit, Unit> SendMagicLinkCommand { get; }

    public IReadOnlyList<string> AppearanceOptions { get; } = new[] { "Claro", "Escuro" };
    public IReadOnlyList<string> PrimaryColorOptions { get; } = new[] { "Índigo", "Azul", "Verde", "Roxo", "Coral" };
    public IReadOnlyList<string> MeasurementDisplayFormatOptions { get; } = new[] { "13/8", "130/80" };

    private string _selectedAppearance = "Claro";
    public string SelectedAppearance { get => _selectedAppearance; set => this.RaiseAndSetIfChanged(ref _selectedAppearance, value); }

    private string _selectedPrimaryColor = "Índigo";
    public string SelectedPrimaryColor { get => _selectedPrimaryColor; set => this.RaiseAndSetIfChanged(ref _selectedPrimaryColor, value); }

    private string _selectedDisplayFormat = "13/8";
    public string SelectedDisplayFormat { get => _selectedDisplayFormat; set => this.RaiseAndSetIfChanged(ref _selectedDisplayFormat, value); }

    private string _exportStatus = string.Empty;
    public string ExportStatus { get => _exportStatus; set => this.RaiseAndSetIfChanged(ref _exportStatus, value); }

    private string _syncDirectory = string.Empty;
    public string SyncDirectory { get => _syncDirectory; set => this.RaiseAndSetIfChanged(ref _syncDirectory, value); }

    private string _syncStatus = string.Empty;
    public string SyncStatus { get => _syncStatus; set => this.RaiseAndSetIfChanged(ref _syncStatus, value); }

    private string _supabaseUrl = string.Empty;
    public string SupabaseUrl { get => _supabaseUrl; set => this.RaiseAndSetIfChanged(ref _supabaseUrl, value); }

    private string _supabaseAnonKey = string.Empty;
    public string SupabaseAnonKey { get => _supabaseAnonKey; set => this.RaiseAndSetIfChanged(ref _supabaseAnonKey, value); }

    private string _authEmail = string.Empty;
    public string AuthEmail { get => _authEmail; set => this.RaiseAndSetIfChanged(ref _authEmail, value); }

    private string _authPassword = string.Empty;
    public string AuthPassword { get => _authPassword; set => this.RaiseAndSetIfChanged(ref _authPassword, value); }

    private bool _isAuthenticated;
    public bool IsAuthenticated { get => _isAuthenticated; set => this.RaiseAndSetIfChanged(ref _isAuthenticated, value); }

    private string _authUserEmail = string.Empty;
    public string AuthUserEmail { get => _authUserEmail; set => this.RaiseAndSetIfChanged(ref _authUserEmail, value); }
}
