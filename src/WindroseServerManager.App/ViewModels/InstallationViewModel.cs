using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindroseServerManager.App.Services;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public partial class InstallationViewModel : ViewModelBase
{
    private readonly IServerInstallService _install;
    private readonly IAppSettingsService _settings;
    private readonly IToastService _toasts;
    private readonly IUpdateCheckService _updateCheck;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private string _installDir = string.Empty;
    [ObservableProperty] private ServerInstallInfo? _installInfo;
    [ObservableProperty] private bool _isInstalling;
    [ObservableProperty] private string _currentPhase = string.Empty;
    [ObservableProperty] private double? _progressPercent;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _binaryPath;
    [ObservableProperty] private bool? _hasUpdate;
    [ObservableProperty] private string? _updateMessage;
    [ObservableProperty] private bool _isCheckingUpdate;

    public bool CanOpenInstallDir => !string.IsNullOrWhiteSpace(InstallDir) && Directory.Exists(InstallDir);

    public ObservableCollection<string> Log { get; } = new();

    public string InstalledStatusText =>
        InstallInfo?.IsInstalled == true
            ? Loc.Get("Status.Installed")
            : (InstallInfo is null ? Loc.Get("Installation.NotCheckedYet") : Loc.Get("Status.NotInstalled"));

    public string BuildIdDisplay => InstallInfo is null ? string.Empty : Loc.Format("Installation.BuildIdFormat", InstallInfo.BuildId);
    public string BinaryDisplay => string.IsNullOrEmpty(BinaryPath)
        ? Loc.Get("Installation.BinaryPlaceholder")
        : Loc.Format("Installation.BinaryFormat", BinaryPath);
    public string LastUpdatedDisplay =>
        InstallInfo is null ? string.Empty : Loc.Format("Installation.LastUpdatedFormat", InstallInfo.LastUpdatedUtc);
    public string UpdateMessageDisplay =>
        string.IsNullOrEmpty(UpdateMessage) ? Loc.Get("Installation.UpdateStatusPlaceholder") : UpdateMessage!;

    partial void OnInstallInfoChanged(ServerInstallInfo? value)
    {
        OnPropertyChanged(nameof(InstalledStatusText));
        OnPropertyChanged(nameof(BuildIdDisplay));
        OnPropertyChanged(nameof(LastUpdatedDisplay));
    }

    partial void OnBinaryPathChanged(string? value) => OnPropertyChanged(nameof(BinaryDisplay));
    partial void OnUpdateMessageChanged(string? value) => OnPropertyChanged(nameof(UpdateMessageDisplay));

    public InstallationViewModel(
        IServerInstallService install,
        IAppSettingsService settings,
        IToastService toasts,
        IUpdateCheckService updateCheck,
        ILocalizationService localization)
    {
        _install = install;
        _settings = settings;
        _toasts = toasts;
        _updateCheck = updateCheck;
        InstallDir = settings.Current.ServerInstallDir;
        localization.LanguageChanged += () =>
        {
            OnPropertyChanged(nameof(InstalledStatusText));
            OnPropertyChanged(nameof(BuildIdDisplay));
            OnPropertyChanged(nameof(BinaryDisplay));
            OnPropertyChanged(nameof(LastUpdatedDisplay));
            OnPropertyChanged(nameof(UpdateMessageDisplay));
        };
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        IsCheckingUpdate = true;
        try
        {
            var result = await _updateCheck.CheckAsync(InstallDir);
            HasUpdate = result.HasUpdate;
            UpdateMessage = result.Message;
            if (!string.IsNullOrWhiteSpace(result.Message))
            {
                if (result.HasUpdate) _toasts.Warning(result.Message);
                else _toasts.Info(result.Message);
            }
        }
        catch (Exception ex)
        {
            _toasts.Error(Loc.Format("Toast.UpdateCheckFailedFormat", ErrorMessageHelper.FriendlyMessage(ex)));
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (string.IsNullOrWhiteSpace(InstallDir)) { InstallInfo = null; BinaryPath = null; OnPropertyChanged(nameof(CanOpenInstallDir)); return; }
        try
        {
            InstallInfo = await _install.GetInstallInfoAsync(InstallDir);
            BinaryPath = ServerInstallService.FindServerBinary(InstallDir);
            OnPropertyChanged(nameof(CanOpenInstallDir));
        }
        catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); }
    }

    partial void OnInstallDirChanged(string value) => OnPropertyChanged(nameof(CanOpenInstallDir));

    [RelayCommand]
    private void OpenInstallDir()
    {
        if (!CanOpenInstallDir) return;
        try { Process.Start(new ProcessStartInfo { FileName = InstallDir, UseShellExecute = true }); } catch { }
    }

    [RelayCommand]
    private async Task PickFolderAsync()
    {
        var top = Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d ? d.MainWindow : null;
        if (top is null) return;
        var picks = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Loc.Get("Installation.PickFolder.Title"),
            AllowMultiple = false,
        });
        if (picks.Count > 0)
        {
            InstallDir = picks[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        ErrorMessage = _install.ValidateInstallDir(InstallDir);
        if (ErrorMessage is not null) { _toasts.Warning(ErrorMessage); return; }

        await _settings.UpdateAsync(s => s.ServerInstallDir = InstallDir);
        Log.Clear();
        IsInstalling = true;
        _cts = new CancellationTokenSource();
        var completeToastShown = false;
        var failedToastShown = false;
        try
        {
            await foreach (var p in _install.InstallOrUpdateAsync(InstallDir, _cts.Token))
            {
                CurrentPhase = $"{Loc.Get($"InstallPhase.{p.Phase}")}: {p.Message}";
                ProgressPercent = p.Percent;
                if (!string.IsNullOrWhiteSpace(p.LogLine))
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        Log.Add(p.LogLine);
                        if (Log.Count > 500) Log.RemoveAt(0);
                    });
                if (p.Phase == InstallPhase.Failed && !failedToastShown)
                {
                    ErrorMessage = p.Message;
                    _toasts.Error(p.Message);
                    failedToastShown = true;
                }
                else if (p.Phase == InstallPhase.Complete && !completeToastShown)
                {
                    _toasts.Success(Loc.Get("Installation.Complete"));
                    completeToastShown = true;
                }
            }
            await RefreshAsync();
        }
        catch (OperationCanceledException) { CurrentPhase = Loc.Get("Installation.Cancelled"); _toasts.Info(Loc.Get("Toast.Cancelled")); }
        catch (Exception ex) { var msg = ErrorMessageHelper.FriendlyMessage(ex); ErrorMessage = msg; _toasts.Error(msg); }
        finally
        {
            IsInstalling = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();
}
