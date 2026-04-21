using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindroseServerManager.App.Services;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public partial class RetrofitBannerViewModel : ViewModelBase, IWindrosePlusOptInContext
{
    private readonly IWindrosePlusService _wplus;
    private readonly IWindrosePlusApiService _wplusApi;
    private readonly IAppSettingsService _settings;
    private readonly IToastService _toasts;

    public string ServerInstallDir { get; }

    [ObservableProperty] private bool _isOptingIn = true;
    [ObservableProperty] private string _rconPassword = string.Empty;
    [ObservableProperty] private int _dashboardPort;
    [ObservableProperty] private string _adminSteamId = string.Empty;
    [ObservableProperty] private bool _hasSteamIdError;
    [ObservableProperty] private bool _isInstalling;
    [ObservableProperty] private string _currentPhase = string.Empty;
    [ObservableProperty] private string? _errorMessage;

    /// <summary>
    /// True when opt-in is active but the Steam-ID field is still empty.
    /// Drives the required-field caption in WindrosePlusOptInControl.
    /// </summary>
    public bool IsSteamIdMissing => IsOptingIn && string.IsNullOrWhiteSpace(AdminSteamId);

    /// <summary>
    /// Raised after opt-out (Dismiss) or install completion so DashboardViewModel
    /// can re-evaluate banner visibility without waiting for the next timer tick.
    /// </summary>
    public event Action? StateChanged;

    public RetrofitBannerViewModel(
        string serverInstallDir,
        IWindrosePlusService wplus,
        IWindrosePlusApiService wplusApi,
        IAppSettingsService settings,
        IToastService toasts)
    {
        ServerInstallDir = serverInstallDir;
        _wplus = wplus;
        _wplusApi = wplusApi;
        _settings = settings;
        _toasts = toasts;
        RconPassword = RconPasswordGenerator.Generate(24);
        DashboardPort = FreePortProbe.FindFreePort();
    }

    partial void OnIsOptingInChanged(bool value)
    {
        if (!value) HasSteamIdError = false;
        OnPropertyChanged(nameof(IsSteamIdMissing));
    }

    partial void OnAdminSteamIdChanged(string value)
    {
        OnPropertyChanged(nameof(IsSteamIdMissing));
    }

    public void RegeneratePassword() => RconPassword = RconPasswordGenerator.Generate(24);

    public void ValidateSteamId()
    {
        if (!IsOptingIn)
        {
            HasSteamIdError = false;
            return;
        }
        if (string.IsNullOrWhiteSpace(AdminSteamId))
        {
            HasSteamIdError = false; // empty = not yet filled, not invalid
            return;
        }
        HasSteamIdError = SteamIdParser.ExtractSteamId64(AdminSteamId) is null;
    }

    public bool CanConfirmInstall()
        => !IsInstalling
           && (!IsOptingIn
               || (!string.IsNullOrWhiteSpace(AdminSteamId) && !HasSteamIdError));

    [RelayCommand]
    private async Task OpenRetrofitDialogAsync()
    {
        var owner = (Avalonia.Application.Current?.ApplicationLifetime
            as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is null) return;

        var dialog = new WindroseServerManager.App.Views.Dialogs.RetrofitDialog { DataContext = this };
        var confirmed = await dialog.ShowDialog<bool>(owner);

        if (confirmed)
            await RunInstallAsync();
        // If user cancelled, banner stays (state=NeverAsked) — re-evaluated on next refresh.

        StateChanged?.Invoke();
    }

    [RelayCommand]
    private async Task DismissOptOutAsync()
    {
        await _settings.UpdateAsync(s =>
        {
            s.WindrosePlusOptInStateByServer[ServerInstallDir] = OptInState.OptedOut;
        });
        StateChanged?.Invoke();
    }

    private async Task RunInstallAsync()
    {
        IsInstalling = true;
        ErrorMessage = null;
        try
        {
            if (IsOptingIn)
            {
                var progress = new Progress<InstallProgress>(p =>
                {
                    CurrentPhase = p.Phase switch
                    {
                        InstallPhase.DownloadingArchive => Loc.Get("Wizard.WindrosePlus.Progress.Downloading"),
                        InstallPhase.Extracting => Loc.Get("Wizard.WindrosePlus.Progress.Extracting"),
                        InstallPhase.Installing => Loc.Get("Wizard.WindrosePlus.Progress.Installing"),
                        _ => string.IsNullOrWhiteSpace(p.Message)
                            ? Loc.Get($"InstallPhase.{p.Phase}")
                            : p.Message,
                    };
                });
                await _wplus.InstallAsync(ServerInstallDir, progress, CancellationToken.None);

                // Write windrose_plus.json so WindrosePlus uses the configured port/password.
                var cfg = _wplusApi.ReadConfig(ServerInstallDir) ?? new WindroseServerManager.Core.Models.WindrosePlusConfig();
                cfg.Server["http_port"] = DashboardPort;
                cfg.Rcon["enabled"] = true;
                cfg.Rcon["password"] = RconPassword;
                await _wplusApi.WriteConfigAsync(ServerInstallDir, cfg, CancellationToken.None);
            }

            var state = IsOptingIn ? OptInState.OptedIn : OptInState.OptedOut;
            await _settings.UpdateAsync(s =>
            {
                s.WindrosePlusActiveByServer[ServerInstallDir] = IsOptingIn;
                s.WindrosePlusOptInStateByServer[ServerInstallDir] = state;
                if (IsOptingIn)
                {
                    s.WindrosePlusRconPasswordByServer[ServerInstallDir] = RconPassword;
                    s.WindrosePlusDashboardPortByServer[ServerInstallDir] = DashboardPort;
                    var parsed = SteamIdParser.ExtractSteamId64(AdminSteamId);
                    if (parsed is not null)
                        s.WindrosePlusAdminSteamIdByServer[ServerInstallDir] = parsed;
                }
            });

            _toasts.Success(Loc.Get("Wizard.WindrosePlus.InstallSuccess"));
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = null;
        }
        catch (WindrosePlusOfflineException)
        {
            ErrorMessage = Loc.Get("Error.WindrosePlus.NoInternet");
            _toasts.Error(ErrorMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = ErrorMessageHelper.FriendlyMessage(ex);
            if (string.IsNullOrWhiteSpace(ErrorMessage))
                ErrorMessage = Loc.Get("Error.WindrosePlus.InstallFailed");
            _toasts.Error(ErrorMessage);
        }
        finally
        {
            IsInstalling = false;
        }
    }
}
