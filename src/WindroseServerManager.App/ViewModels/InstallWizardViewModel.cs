using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindroseServerManager.App.Services;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public partial class InstallWizardViewModel : ViewModelBase, IWindrosePlusOptInContext
{
    private readonly IServerInstallService _install;
    private readonly IWindrosePlusService _wplus;
    private readonly IAppSettingsService _settings;
    private readonly IToastService _toasts;
    private CancellationTokenSource? _cts;

    public event Action<bool>? CloseRequested;

    [ObservableProperty] private int _currentStep = 1;
    [ObservableProperty] private string _installDir = string.Empty;
    [ObservableProperty] private bool _isOptingIn = true;
    [ObservableProperty] private string _rconPassword = string.Empty;
    [ObservableProperty] private int _dashboardPort;
    [ObservableProperty] private string _adminSteamId = string.Empty;
    [ObservableProperty] private bool _hasSteamIdError;
    [ObservableProperty] private bool _isInstalling;
    [ObservableProperty] private string _currentPhase = string.Empty;
    [ObservableProperty] private string? _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public InstallWizardViewModel(
        IServerInstallService install,
        IWindrosePlusService wplus,
        IAppSettingsService settings,
        IToastService toasts,
        ILocalizationService localization)
    {
        _install = install;
        _wplus = wplus;
        _settings = settings;
        _toasts = toasts;
        _ = localization; // keep the ctor signature open for future language-change hooks

        RconPassword = RconPasswordGenerator.Generate(24);
        DashboardPort = FreePortProbe.FindFreePort();
        InstallDir = settings.Current.ServerInstallDir ?? string.Empty;
    }

    partial void OnCurrentStepChanged(int value)
    {
        GoNextCommand.NotifyCanExecuteChanged();
        GoBackCommand.NotifyCanExecuteChanged();
    }

    partial void OnInstallDirChanged(string value) => GoNextCommand.NotifyCanExecuteChanged();
    partial void OnIsOptingInChanged(bool value)
    {
        if (!value) HasSteamIdError = false;
        GoNextCommand.NotifyCanExecuteChanged();
    }
    partial void OnAdminSteamIdChanged(string value) => GoNextCommand.NotifyCanExecuteChanged();
    partial void OnHasSteamIdErrorChanged(bool value) => GoNextCommand.NotifyCanExecuteChanged();
    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void GoNext()
    {
        if (CurrentStep < 3) CurrentStep++;
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack()
    {
        if (CurrentStep > 1) CurrentStep--;
    }

    private bool CanGoBack() => CurrentStep > 1 && !IsInstalling;

    private bool CanGoNext() => CurrentStep switch
    {
        1 => !string.IsNullOrWhiteSpace(InstallDir) && _install.ValidateInstallDir(InstallDir) is null,
        2 => !IsOptingIn || (!string.IsNullOrWhiteSpace(AdminSteamId) && !HasSteamIdError && SteamIdParser.ExtractSteamId64(AdminSteamId) is not null),
        _ => false,
    };

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

    public void RegeneratePassword() => RconPassword = RconPasswordGenerator.Generate(24);

    public void RequestCancelInstall() => _cts?.Cancel();

    [RelayCommand]
    private async Task InstallAsync()
    {
        _cts = new CancellationTokenSource();
        IsInstalling = true;
        ErrorMessage = null;
        try
        {
            // 1) SteamCMD install (Windrose server)
            await foreach (var p in _install.InstallOrUpdateAsync(InstallDir, _cts.Token))
            {
                CurrentPhase = string.IsNullOrWhiteSpace(p.Message)
                    ? Loc.Get($"InstallPhase.{p.Phase}")
                    : $"{Loc.Get($"InstallPhase.{p.Phase}")}: {p.Message}";

                if (p.Phase == InstallPhase.Failed)
                {
                    ErrorMessage = p.Message;
                    _toasts.Error(p.Message);
                    return;
                }
            }

            // 2) WindrosePlus (only if opted in)
            if (IsOptingIn)
            {
                var progress = new Progress<InstallProgress>(p =>
                {
                    CurrentPhase = p.Phase switch
                    {
                        InstallPhase.DownloadingArchive => Loc.Get("Wizard.WindrosePlus.Progress.Downloading"),
                        InstallPhase.Extracting => Loc.Get("Wizard.WindrosePlus.Progress.Extracting"),
                        InstallPhase.Installing => Loc.Get("Wizard.WindrosePlus.Progress.Installing"),
                        _ => string.IsNullOrWhiteSpace(p.Message) ? Loc.Get($"InstallPhase.{p.Phase}") : p.Message,
                    };
                });
                await _wplus.InstallAsync(InstallDir, progress, _cts.Token);
            }

            // 3) Persist per-server state
            var state = IsOptingIn ? OptInState.OptedIn : OptInState.OptedOut;
            await _settings.UpdateAsync(s =>
            {
                s.ServerInstallDir = InstallDir;
                s.WindrosePlusActiveByServer[InstallDir] = IsOptingIn;
                s.WindrosePlusOptInStateByServer[InstallDir] = state;
                if (IsOptingIn)
                {
                    s.WindrosePlusRconPasswordByServer[InstallDir] = RconPassword;
                    s.WindrosePlusDashboardPortByServer[InstallDir] = DashboardPort;
                    var parsed = SteamIdParser.ExtractSteamId64(AdminSteamId);
                    if (parsed is not null)
                        s.WindrosePlusAdminSteamIdByServer[InstallDir] = parsed;
                }
            }, _cts.Token);

            _toasts.Success(Loc.Get("Wizard.WindrosePlus.InstallSuccess"));
            CloseRequested?.Invoke(true);
        }
        catch (OperationCanceledException)
        {
            // user cancelled — keep dialog open, clear error
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
            _cts?.Dispose();
            _cts = null;
        }
    }
}
