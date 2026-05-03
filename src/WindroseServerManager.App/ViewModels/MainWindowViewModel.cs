using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WindroseServerManager.App.Services;
using WindroseServerManager.Core.Models;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public sealed class NavItem : ObservableObject
{
    public string TitleKey { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public Type VmType { get; init; } = typeof(ViewModelBase);

    public string Title => Loc.Get(TitleKey);

    /// <summary>Called after language change to re-raise the Title binding.</summary>
    public void RefreshTitle() => OnPropertyChanged(nameof(Title));
}

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _nav;
    private readonly IAppSettingsService _settings;
    private readonly ILogger<MainWindowViewModel> _logger;

    public ObservableCollection<NavItem> NavItems { get; }
    public ObservableCollection<NavItem> FooterItems { get; }
    public ObservableCollection<ServerEntry> Servers { get; } = new();
    public IToastService Toasts { get; }

    [ObservableProperty] private ViewModelBase? _currentPage;
    [ObservableProperty] private NavItem? _selectedMainItem;
    [ObservableProperty] private NavItem? _selectedFooterItem;
    [ObservableProperty] private ServerEntry? _activeServer;
    private bool _suppressSelectionSync;

    public bool HasMultipleServers => Servers.Count > 1;

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
        ILocalizationService localization,
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
            new() { TitleKey = "Nav.Dashboard", Icon = "\uE80F", VmType = typeof(DashboardViewModel) },
            new() { TitleKey = "Nav.Server", Icon = "\uE896", VmType = typeof(InstallationViewModel) },
            new() { TitleKey = "Nav.ServerControl", Icon = "\uE756", VmType = typeof(ServerControlViewModel) },
            new() { TitleKey = "Nav.Configuration", Icon = "\uE9E9", VmType = typeof(ConfigurationViewModel) },
            // Phase 11 — WindrosePlus Feature Views
            new() { TitleKey = "Nav.Players",  Icon = "\uE716", VmType = typeof(PlayersViewModel) },
            new() { TitleKey = "Nav.Events",   Icon = "\uE81C", VmType = typeof(EventsViewModel) },
            // Sea Chart view removed — live map is now launched per-server via the Server
            // cards ("Open live map" action), which opens in the default browser where all
            // Leaflet interactions (zoom/pan/wheel) work reliably. The embedded NativeWebView
            // in Avalonia.Controls.WebView 12.0.0 does not route mouse input properly.
            new() { TitleKey = "Nav.Editor",   Icon = "\uE70F", VmType = typeof(EditorViewModel) },
            new() { TitleKey = "Nav.Mods", Icon = "\uEA86", VmType = typeof(ModsViewModel) },
            new() { TitleKey = "Nav.Backups", Icon = "\uE8C8", VmType = typeof(BackupsViewModel) },
        };
        FooterItems = new ObservableCollection<NavItem>
        {
            new() { TitleKey = "Nav.Settings", Icon = "\uE713", VmType = typeof(SettingsViewModel) },
        };

        localization.LanguageChanged += () =>
        {
            foreach (var n in NavItems) n.RefreshTitle();
            foreach (var n in FooterItems) n.RefreshTitle();
            UpdateBannerMessage = _pendingLatestVersion is null
                ? string.Empty
                : Loc.Format("Update.Banner.AvailableFormat", _pendingLatestVersion);
        };

        var hasInstall = !string.IsNullOrWhiteSpace(settings.Current.ServerInstallDir)
                         && System.IO.Directory.Exists(settings.Current.ServerInstallDir);
        SelectedMainItem = hasInstall ? NavItems[0] : NavItems[1];

        appUpdate.UpdateChecked += OnUpdateChecked;

        // Initiale Server-Liste laden
        SyncServersFromSettings(settings.Current);

        // Bei Einstellungsänderungen (z.B. Server hinzugefügt/entfernt) neu synchronisieren
        settings.Changed += current =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => SyncServersFromSettings(current));
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

    partial void OnActiveServerChanged(ServerEntry? value)
    {
        if (value is null) return;
        _ = SelectServerSafeAsync(value.Id);
    }

    private async Task SelectServerSafeAsync(string serverId)
    {
        try
        {
            await _settings.SelectServerAsync(serverId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Server-Wechsel zu {ServerId} fehlgeschlagen", serverId);
        }
    }

    private void SyncServersFromSettings(WindroseServerManager.Core.Models.AppSettings current)
    {
        Servers.Clear();
        foreach (var server in current.Servers)
            Servers.Add(server);

        OnPropertyChanged(nameof(HasMultipleServers));

        // Aktiven Server setzen ohne OnActiveServerChanged auszulösen (kein SelectServerAsync-Call nötig)
        var activeId = current.ActiveServerId;
        var active = Servers.FirstOrDefault(s => s.Id == activeId) ?? Servers.FirstOrDefault();
#pragma warning disable MVVMTK0034
        _activeServer = active;
#pragma warning restore MVVMTK0034
        OnPropertyChanged(nameof(ActiveServer));
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
                Toasts.Info(Loc.Format("Toast.AutoRestartPrefix", evt.Reason));
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
            UpdateBannerMessage = Loc.Format("Update.Banner.AvailableFormat", result.LatestVersion);
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
            Toasts.Error(Loc.Get("Toast.ReleasePageFailed"));
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
