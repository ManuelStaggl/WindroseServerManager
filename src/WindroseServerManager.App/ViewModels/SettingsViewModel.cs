using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindroseServerManager.App.Services;
using WindroseServerManager.App.Views.Dialogs;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IAppSettingsService _settings;
    private readonly IToastService _toasts;
    private readonly IFirewallService _firewall;
    private readonly IAutoStartService _autoStart;
    private readonly IAppUpdateService _appUpdate;

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

    // App-Update-Check
    [ObservableProperty] private bool _isUpdateCheckBusy;
    [ObservableProperty] private string? _updateCheckStatus;

    private bool _suppressAutoStartWrite;

    public SettingsViewModel(
        IAppSettingsService settings,
        IToastService toasts,
        IFirewallService firewall,
        IAutoStartService autoStart,
        IAppUpdateService appUpdate)
    {
        _settings = settings;
        _toasts = toasts;
        _firewall = firewall;
        _autoStart = autoStart;
        _appUpdate = appUpdate;

        var c = settings.Current;
        _autoRestartOnCrash = c.AutoRestartOnCrash;
        _gracefulShutdownSeconds = c.GracefulShutdownSeconds;

        _logEnabled = c.LogEnabled;
        _extraLaunchArgs = c.ExtraLaunchArgs;

        _steamAppId = c.SteamAppId;
        _steamLogin = c.SteamLogin;

        _suppressAutoStartWrite = true;
        _autoStartEnabled = _autoStart.IsEnabled();
        _suppressAutoStartWrite = false;

        _settings.Changed += OnSettingsChanged;
        _ = CheckFirewallCoreAsync(showToast: false);
    }

    [RelayCommand]
    private Task CheckFirewallAsync() => CheckFirewallCoreAsync(showToast: true);

    private async Task CheckFirewallCoreAsync(bool showToast)
    {
        var binary = ResolveServerBinary();
        if (string.IsNullOrWhiteSpace(binary))
        {
            IsFirewallRuleInstalled = false;
            if (showToast) _toasts.Warning("Server-Binary nicht gefunden. Bitte zuerst installieren.");
            return;
        }
        try
        {
            IsFirewallBusy = true;
            IsFirewallRuleInstalled = await _firewall.IsRuleInstalledAsync(binary);
            if (showToast)
            {
                if (IsFirewallRuleInstalled) _toasts.Success("Firewall-Regel ist aktiv.");
                else _toasts.Info("Keine Firewall-Regel vorhanden.");
            }
        }
        catch (Exception ex)
        {
            if (showToast) _toasts.Error($"Status-Prüfung fehlgeschlagen: {ErrorMessageHelper.FriendlyMessage(ex)}");
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
            _toasts.Warning("Server-Binary nicht gefunden. Bitte zuerst installieren.");
            return;
        }

        if (!FirewallService.IsCurrentProcessElevated())
        {
            _toasts.Warning("App mit Admin-Rechten neu starten nötig.");
            return;
        }

        try
        {
            IsFirewallBusy = true;
            bool ok;
            if (IsFirewallRuleInstalled)
            {
                ok = await _firewall.RemoveRuleAsync(binary);
                if (ok) _toasts.Success("Firewall-Regel entfernt.");
                else _toasts.Error("Firewall-Regel konnte nicht entfernt werden.");
            }
            else
            {
                ok = await _firewall.InstallRuleAsync(binary);
                if (ok) _toasts.Success("Firewall-Regel hinzugefügt.");
                else _toasts.Error("Firewall-Regel konnte nicht hinzugefügt werden.");
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
        => ServerInstallService.FindServerBinary(_settings.Current.ServerInstallDir);

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
                    _toasts.Warning("Pfad zur App konnte nicht ermittelt werden.");
                    return;
                }
                _autoStart.Enable(exe);
                _toasts.Success("Autostart aktiviert.");
            }
            else
            {
                _autoStart.Disable();
                _toasts.Info("Autostart deaktiviert.");
            }
        }
        catch (Exception ex)
        {
            _toasts.Error($"Autostart-Fehler: {ErrorMessageHelper.FriendlyMessage(ex)}");
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
        StatusMessage = "Einstellungen gespeichert.";
        _toasts.Success("Einstellungen gespeichert.");
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
            UpdateCheckStatus = "Prüfe auf Updates…";
            var result = await _appUpdate.CheckAsync();
            UpdateCheckStatus = result.Message;
            if (result.HasUpdate) _toasts.Info(result.Message);
            else _toasts.Success(result.Message);
        }
        catch (Exception ex)
        {
            UpdateCheckStatus = "Update-Check fehlgeschlagen.";
            _toasts.Error(ErrorMessageHelper.FriendlyMessage(ex));
        }
        finally
        {
            IsUpdateCheckBusy = false;
        }
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
