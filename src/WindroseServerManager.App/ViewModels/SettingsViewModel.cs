using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindroseServerManager.App.Services;
using WindroseServerManager.App.Views.Dialogs;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public sealed class LanguageOption
{
    public required string Key { get; init; }        // "auto" | "de" | "en"
    public required string DisplayName { get; init; }
}

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IAppSettingsService _settings;
    private readonly IToastService _toasts;
    private readonly IFirewallService _firewall;
    private readonly IAutoStartService _autoStart;
    private readonly IAppUpdateService _appUpdate;
    private readonly ILocalizationService _localization;
    private readonly IWindrosePlusService _wplus;
    private readonly IWindrosePlusApiService _wplusApi;

    [ObservableProperty] private bool _autoRestartOnCrash;
    [ObservableProperty] private int _gracefulShutdownSeconds;

    // Launch-Args (strukturiert)
    [ObservableProperty] private bool _logEnabled = true;
    [ObservableProperty] private string _extraLaunchArgs = string.Empty;

    // Steam
    [ObservableProperty] private string _steamAppId = "4129620";
    [ObservableProperty] private string _steamLogin = string.Empty;

    [ObservableProperty] private string? _statusMessage;

    // Firewall
    [ObservableProperty] private bool _isFirewallRuleInstalled;
    [ObservableProperty] private bool _isFirewallBusy;

    // Autostart
    [ObservableProperty] private bool _autoStartEnabled;
    [ObservableProperty] private bool _autoStartServerOnAppLaunch;

    // App-Update-Check
    [ObservableProperty] private bool _isUpdateCheckBusy;
    [ObservableProperty] private string? _updateCheckStatus;
    [ObservableProperty] private bool _hasUpdateAvailable;
    [ObservableProperty] private string? _pendingReleaseUrl;
    [ObservableProperty] private string? _pendingDownloadUrl;

    // Language
    public ObservableCollection<LanguageOption> LanguageOptions { get; } = new();
    [ObservableProperty] private LanguageOption? _selectedLanguageOption;
    private bool _suppressLanguageWrite;

    private bool _suppressAutoStartWrite;

    public SettingsViewModel(
        IAppSettingsService settings,
        IToastService toasts,
        IFirewallService firewall,
        IAutoStartService autoStart,
        IAppUpdateService appUpdate,
        ILocalizationService localization,
        IWindrosePlusService wplus,
        IWindrosePlusApiService wplusApi)
    {
        _settings = settings;
        _toasts = toasts;
        _firewall = firewall;
        _autoStart = autoStart;
        _appUpdate = appUpdate;
        _localization = localization;
        _wplus = wplus;
        _wplusApi = wplusApi;

        var c = settings.Current;
        _autoRestartOnCrash = c.AutoRestartOnCrash;
        _gracefulShutdownSeconds = c.GracefulShutdownSeconds;

        _logEnabled = c.LogEnabled;
        _extraLaunchArgs = c.ExtraLaunchArgs;

        _steamAppId = c.SteamAppId;
        _steamLogin = c.SteamLogin;

        _suppressAutoStartWrite = true;
        _autoStartEnabled = _autoStart.IsEnabled();
        _autoStartServerOnAppLaunch = c.AutoStartServerOnAppLaunch;
        _suppressAutoStartWrite = false;

        RebuildLanguageOptions();
        _localization.LanguageChanged += OnLanguageChanged;

        _settings.Changed += OnSettingsChanged;
        _ = CheckFirewallCoreAsync(showToast: false);
    }

    public string AppVersionDisplay =>
        Loc.Format("Settings.About.VersionFormat",
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0");

    private void OnLanguageChanged()
    {
        RebuildLanguageOptions();
        OnPropertyChanged(nameof(AppVersionDisplay));
    }

    private void RebuildLanguageOptions()
    {
        _suppressLanguageWrite = true;
        try
        {
            LanguageOptions.Clear();
            LanguageOptions.Add(new LanguageOption { Key = "auto", DisplayName = Loc.Get("Settings.Language.Auto") });
            LanguageOptions.Add(new LanguageOption { Key = "de",   DisplayName = Loc.Get("Settings.Language.German") });
            LanguageOptions.Add(new LanguageOption { Key = "en",   DisplayName = Loc.Get("Settings.Language.English") });

            var current = _localization.CurrentSetting;
            SelectedLanguageOption = LanguageOptions.FirstOrDefault(o => o.Key == current) ?? LanguageOptions[0];
        }
        finally
        {
            _suppressLanguageWrite = false;
        }
    }

    partial void OnSelectedLanguageOptionChanged(LanguageOption? value)
    {
        if (_suppressLanguageWrite || value is null) return;
        if (string.Equals(value.Key, _localization.CurrentSetting, StringComparison.OrdinalIgnoreCase)) return;

        _localization.SetLanguage(value.Key);
        _ = _settings.UpdateAsync(s => s.Language = value.Key);
    }

    [RelayCommand]
    private Task CheckFirewallAsync() => CheckFirewallCoreAsync(showToast: true);

    private async Task CheckFirewallCoreAsync(bool showToast)
    {
        var binary = ResolveServerBinary();
        if (string.IsNullOrWhiteSpace(binary))
        {
            IsFirewallRuleInstalled = false;
            if (showToast) _toasts.Warning(Loc.Get("Toast.FirewallBinaryMissing"));
            return;
        }
        try
        {
            IsFirewallBusy = true;
            IsFirewallRuleInstalled = await _firewall.IsRuleInstalledAsync(binary);
            if (showToast)
            {
                if (IsFirewallRuleInstalled) _toasts.Success(Loc.Get("Toast.FirewallRuleActive"));
                else _toasts.Info(Loc.Get("Toast.FirewallNoRule"));
            }
        }
        catch (Exception ex)
        {
            if (showToast) _toasts.Error(Loc.Format("Toast.FirewallCheckFailedFormat", ErrorMessageHelper.FriendlyMessage(ex)));
        }
        finally
        {
            IsFirewallBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleFirewallAsync()
    {
        var binary = ResolveServerBinary();
        if (string.IsNullOrWhiteSpace(binary))
        {
            _toasts.Warning(Loc.Get("Toast.FirewallBinaryMissing"));
            return;
        }

        if (!FirewallService.IsCurrentProcessElevated())
        {
            _toasts.Warning(Loc.Get("Toast.FirewallAdminNeeded"));
            return;
        }

        try
        {
            IsFirewallBusy = true;
            bool ok;
            if (IsFirewallRuleInstalled)
            {
                ok = await _firewall.RemoveRuleAsync(binary);
                if (ok) _toasts.Success(Loc.Get("Toast.FirewallRuleRemoved"));
                else _toasts.Error(Loc.Get("Toast.FirewallRuleNotRemoved"));
            }
            else
            {
                ok = await _firewall.InstallRuleAsync(binary);
                if (ok) _toasts.Success(Loc.Get("Toast.FirewallRuleAdded"));
                else _toasts.Error(Loc.Get("Toast.FirewallRuleNotAdded"));
            }
            await CheckFirewallCoreAsync(showToast: false);
        }
        finally
        {
            IsFirewallBusy = false;
        }
    }

    private void OnSettingsChanged(WindroseServerManager.Core.Models.AppSettings settings)
    {
        _ = CheckFirewallCoreAsync(showToast: false);
    }

    private string? ResolveServerBinary()
        => ServerInstallService.FindServerBinary(_settings.ActiveServerDir);

    partial void OnAutoStartServerOnAppLaunchChanged(bool value)
    {
        if (_suppressAutoStartWrite) return;
        _ = _settings.UpdateAsync(s => s.AutoStartServerOnAppLaunch = value);
    }

    partial void OnAutoStartEnabledChanged(bool value)
    {
        if (_suppressAutoStartWrite) return;
        try
        {
            if (value)
            {
                var exe = Environment.ProcessPath
                    ?? System.Reflection.Assembly.GetEntryAssembly()?.Location
                    ?? string.Empty;
                if (string.IsNullOrWhiteSpace(exe))
                {
                    _toasts.Warning(Loc.Get("Toast.AppPathUnknown"));
                    return;
                }
                _autoStart.Enable(exe);
                _toasts.Success(Loc.Get("Toast.AutoStartOn"));
            }
            else
            {
                _autoStart.Disable();
                _toasts.Info(Loc.Get("Toast.AutoStartOff"));
            }
        }
        catch (Exception ex)
        {
            _toasts.Error(Loc.Format("Toast.AutoStartErrorFormat", ErrorMessageHelper.FriendlyMessage(ex)));
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _settings.UpdateAsync(s =>
        {
            s.AutoRestartOnCrash = AutoRestartOnCrash;
            s.GracefulShutdownSeconds = Math.Max(5, GracefulShutdownSeconds);

            s.LogEnabled = LogEnabled;
            s.ExtraLaunchArgs = ExtraLaunchArgs?.Trim() ?? string.Empty;

            s.SteamAppId = string.IsNullOrWhiteSpace(SteamAppId) ? "4129620" : SteamAppId.Trim();
            s.SteamLogin = SteamLogin?.Trim() ?? string.Empty;
        });
        StatusMessage = Loc.Get("Toast.SettingsSaved");
        _toasts.Success(Loc.Get("Toast.SettingsSaved"));
    }

    public string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    [RelayCommand]
    private async Task CheckAppUpdateAsync()
    {
        if (IsUpdateCheckBusy) return;
        try
        {
            IsUpdateCheckBusy = true;
            UpdateCheckStatus = Loc.Get("Toast.UpdateChecking");
            HasUpdateAvailable = false;
            PendingReleaseUrl = null;
            PendingDownloadUrl = null;

            var result = await _appUpdate.CheckAsync();
            UpdateCheckStatus = result.Message;
            HasUpdateAvailable = result.HasUpdate;
            PendingReleaseUrl = result.ReleaseUrl;
            PendingDownloadUrl = result.DownloadUrl;

            if (result.HasUpdate) _toasts.Info(result.Message);
            else _toasts.Success(result.Message);
        }
        catch (Exception ex)
        {
            UpdateCheckStatus = Loc.Get("Toast.UpdateCheckFailed");
            _toasts.Error(ErrorMessageHelper.FriendlyMessage(ex));
        }
        finally
        {
            IsUpdateCheckBusy = false;
        }
    }

    [RelayCommand]
    private void DownloadAppUpdate()
    {
        var url = PendingDownloadUrl ?? PendingReleaseUrl;
        if (string.IsNullOrWhiteSpace(url)) return;
        TryOpenUrl(url);
    }

    [RelayCommand]
    private void OpenReleasePage()
    {
        var url = PendingReleaseUrl ?? PendingDownloadUrl;
        if (string.IsNullOrWhiteSpace(url)) return;
        TryOpenUrl(url);
    }

    private void TryOpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _toasts.Error(Loc.Get("Toast.ReleasePageFailed"));
            System.Diagnostics.Debug.WriteLine($"Failed to open URL {url}: {ex.Message}");
        }
    }

    // ── WindrosePlus ────────────────────────────────────────────
    public bool HasServerConfigured =>
        !string.IsNullOrWhiteSpace(_settings.ActiveServerDir);

    public string RconPassword
    {
        get
        {
            var dir = _settings.ActiveServerDir;
            if (string.IsNullOrWhiteSpace(dir)) return string.Empty;

            // Try windrose_plus.json first (JsonElement-safe extraction)
            var config = _wplusApi.ReadConfig(dir);
            if (config?.Rcon.TryGetValue("password", out var pw) == true && pw is not null)
            {
                var s = pw is System.Text.Json.JsonElement el ? el.GetString() : pw as string;
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }

            // Fallback: password stored in app settings (set via install wizard)
            return _settings.Current.WindrosePlusRconPasswordByServer
                .GetValueOrDefault(dir, string.Empty);
        }
    }

    [ObservableProperty] private bool _showRconPassword;

    [RelayCommand]
    private void ToggleShowRconPassword() => ShowRconPassword = !ShowRconPassword;

    [RelayCommand]
    private async Task CopyRconPasswordAsync()
    {
        var pw = RconPassword;
        if (string.IsNullOrEmpty(pw)) return;
        var top = Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d ? d.MainWindow : null;
        if (top?.Clipboard is null) return;
        await top.Clipboard.SetTextAsync(pw);
        _toasts.Success(Loc.Get("Settings.WindrosePlus.RconPasswordCopied"));
    }

    public string WindrosePlusStatusText
    {
        get
        {
            var dir = _settings.ActiveServerDir;
            if (string.IsNullOrWhiteSpace(dir)) return Loc.Get("Settings.WindrosePlus.StatusNoServer");
            var active = _settings.Current.WindrosePlusActiveByServer.GetValueOrDefault(dir, false);
            if (active) return Loc.Get("Settings.WindrosePlus.StatusActive");
            var state = _settings.Current.WindrosePlusOptInStateByServer.GetValueOrDefault(dir, OptInState.NeverAsked);
            return state == OptInState.OptedOut
                ? Loc.Get("Settings.WindrosePlus.StatusOptedOut")
                : Loc.Get("Settings.WindrosePlus.StatusNotInstalled");
        }
    }

    [RelayCommand]
    private async Task OpenWindrosePlusDialogAsync()
    {
        var dir = _settings.ActiveServerDir;
        if (string.IsNullOrWhiteSpace(dir)) return;

        var top = Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d ? d.MainWindow : null;
        if (top is null) return;

        var bannerVm = new RetrofitBannerViewModel(dir, _wplus, _wplusApi, _settings, _toasts);
        var dialog = new RetrofitDialog { DataContext = bannerVm };
        var confirmed = await dialog.ShowDialog<bool>(top);
        if (confirmed)
            OnPropertyChanged(nameof(WindrosePlusStatusText));
    }

    [RelayCommand]
    private async Task ShowAboutAsync()
    {
        var top = Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d ? d.MainWindow : null;
        if (top is null) return;
        await AboutDialog.ShowAsync(top);
    }
}
