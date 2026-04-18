using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WindroseServerManager.App.Services;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public sealed class NavItem
{
    public string Title { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public Type VmType { get; init; } = typeof(ViewModelBase);
}

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _nav;
    private readonly IAppSettingsService _settings;
    private readonly ILogger<MainWindowViewModel> _logger;

    public ObservableCollection<NavItem> NavItems { get; }
    public ObservableCollection<NavItem> FooterItems { get; }
    public IToastService Toasts { get; }

    [ObservableProperty] private ViewModelBase? _currentPage;
    [ObservableProperty] private NavItem? _selectedMainItem;
    [ObservableProperty] private NavItem? _selectedFooterItem;
    private bool _suppressSelectionSync;

    // --- App-Update-Banner ---
    [ObservableProperty] private bool _isUpdateBannerVisible;
    [ObservableProperty] private string _updateBannerMessage = string.Empty;
    private string? _pendingLatestVersion;
    private string? _pendingReleaseUrl;

    public MainWindowViewModel(
        INavigationService nav,
        IToastService toasts,
        IAppSettingsService settings,
        IAppUpdateService appUpdate,
        RestartScheduler restartScheduler,
        ILogger<MainWindowViewModel> logger)
    {
        _nav = nav;
        Toasts = toasts;
        _settings = settings;
        _logger = logger;
        _nav.Navigated += vm => CurrentPage = vm;

        restartScheduler.RestartNotified += OnRestartNotified;

        NavItems = new ObservableCollection<NavItem>
        {
            new() { Title = "Dashboard", Icon = "\uE80F", VmType = typeof(DashboardViewModel) },
            new() { Title = "Installation", Icon = "\uE896", VmType = typeof(InstallationViewModel) },
            new() { Title = "Log & Automatisierung", Icon = "\uE756", VmType = typeof(ServerControlViewModel) },
            new() { Title = "Konfiguration", Icon = "\uE9E9", VmType = typeof(ConfigurationViewModel) },
            new() { Title = "Backups", Icon = "\uE8C8", VmType = typeof(BackupsViewModel) },
        };
        FooterItems = new ObservableCollection<NavItem>
        {
            new() { Title = "Einstellungen", Icon = "\uE713", VmType = typeof(SettingsViewModel) },
        };

        var hasInstall = !string.IsNullOrWhiteSpace(settings.Current.ServerInstallDir)
                         && System.IO.Directory.Exists(settings.Current.ServerInstallDir);
        SelectedMainItem = hasInstall ? NavItems[0] : NavItems[1];

        appUpdate.UpdateChecked += OnUpdateChecked;
    }

    partial void OnSelectedMainItemChanged(NavItem? value)
    {
        if (_suppressSelectionSync || value is null) return;
        _suppressSelectionSync = true;
        try { SelectedFooterItem = null; }
        finally { _suppressSelectionSync = false; }
        NavigateToItem(value);
    }

    partial void OnSelectedFooterItemChanged(NavItem? value)
    {
        if (_suppressSelectionSync || value is null) return;
        _suppressSelectionSync = true;
        try { SelectedMainItem = null; }
        finally { _suppressSelectionSync = false; }
        NavigateToItem(value);
    }

    private void NavigateToItem(NavItem item)
    {
        var vm = (ViewModelBase)App.Services.GetService(item.VmType)!;
        _nav.NavigateTo(vm);
    }

    [RelayCommand]
    private void NavigateTo(NavItem item)
    {
        if (NavItems.Contains(item)) SelectedMainItem = item;
        else if (FooterItems.Contains(item)) SelectedFooterItem = item;
    }

    private void OnRestartNotified(WindroseServerManager.Core.Services.RestartEvent evt)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (evt.Trigger == WindroseServerManager.Core.Services.RestartTrigger.ScheduledWarning)
                Toasts.Warning(evt.Reason);
            else
                Toasts.Info($"Auto-Restart: {evt.Reason}");
        });
    }

    private void OnUpdateChecked(AppUpdateResult result)
    {
        // Dispatch auf UI-Thread — Event kommt vom Scheduler-Background-Thread.
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!result.HasUpdate || string.IsNullOrWhiteSpace(result.LatestVersion))
            {
                IsUpdateBannerVisible = false;
                return;
            }

            // Vom User verworfene Version nicht erneut anzeigen.
            if (string.Equals(_settings.Current.DismissedUpdateVersion, result.LatestVersion, StringComparison.Ordinal))
            {
                _logger.LogDebug("Update v{Version} wurde bereits dismissed, Banner bleibt versteckt", result.LatestVersion);
                return;
            }

            _pendingLatestVersion = result.LatestVersion;
            _pendingReleaseUrl = result.ReleaseUrl ?? result.DownloadUrl;
            UpdateBannerMessage = $"Update verfügbar: v{result.LatestVersion}";
            IsUpdateBannerVisible = true;
        });
    }

    [RelayCommand]
    private void DownloadUpdate()
    {
        var url = _pendingReleaseUrl;
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Konnte Release-URL nicht öffnen: {Url}", url);
            Toasts.Error("Release-Seite konnte nicht geöffnet werden.");
        }
    }

    [RelayCommand]
    private async Task DismissUpdateAsync()
    {
        var version = _pendingLatestVersion;
        IsUpdateBannerVisible = false;
        if (string.IsNullOrWhiteSpace(version)) return;

        try
        {
            await _settings.UpdateAsync(s => s.DismissedUpdateVersion = version).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DismissedUpdateVersion konnte nicht gespeichert werden");
        }
    }
}
