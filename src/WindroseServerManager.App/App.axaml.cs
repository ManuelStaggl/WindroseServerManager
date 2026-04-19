using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using WindroseServerManager.App.Services;
using WindroseServerManager.App.ViewModels;
using WindroseServerManager.App.Views;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private IHost? _host;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();
        Services = _host.Services;

        var settings = Services.GetRequiredService<IAppSettingsService>();
        settings.LoadAsync().GetAwaiter().GetResult();

        var localization = Services.GetRequiredService<ILocalizationService>();
        localization.Initialize(settings.Current.Language);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var main = Services.GetRequiredService<MainWindowViewModel>();
            var window = new MainWindow { DataContext = main };

            if (Program.StartMinimizedToTray)
            {
                // Autostart: App bootet in den Tray, kein sichtbares Fenster.
                window.WindowState = Avalonia.Controls.WindowState.Minimized;
                window.ShowInTaskbar = false;
                desktop.MainWindow = window;
            }
            else
            {
                desktop.MainWindow = window;
            }

            desktop.ShutdownRequested += (_, _) =>
            {
                try { _host?.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult(); }
                catch { }
            };
        }

        _ = _host.StartAsync();

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection s)
    {
        s.AddLogging(b => b.ClearProviders().AddSerilog(Log.Logger));
        s.AddHttpClient();

        s.AddSingleton<IAppSettingsService, AppSettingsService>();
        s.AddSingleton<ILocalizationService, LocalizationService>();
        s.AddSingleton<ISteamCmdService, SteamCmdService>();
        s.AddSingleton<IWindrosePlusService, WindrosePlusService>();
        s.AddSingleton<IServerInstallService, ServerInstallService>();
        s.AddSingleton<IServerProcessService, ServerProcessService>();
        s.AddSingleton<IServerConfigService, ServerConfigService>();
        s.AddSingleton<IBackupService, BackupService>();
        s.AddSingleton<IModService, ModService>();
        s.AddSingleton<IMetricsService, MetricsService>();
        s.AddSingleton<IServerEventLog, ServerEventLog>();

        s.AddHostedService<BackupScheduler>();
        s.AddSingleton<RestartScheduler>();
        s.AddHostedService(sp => sp.GetRequiredService<RestartScheduler>());

        s.AddSingleton<INavigationService, NavigationService>();
        s.AddSingleton<IToastService, ToastService>();
        s.AddSingleton<IFirewallService, FirewallService>();
        s.AddSingleton<IUpdateCheckService, UpdateCheckService>();
        s.AddSingleton<IAutoStartService, AutoStartService>();
        s.AddSingleton<IAppUpdateService, AppUpdateService>();
        s.AddHostedService<AppUpdateScheduler>();

        s.AddSingleton<MainWindowViewModel>();

        // (Tray icon handlers below.)
        s.AddSingleton<DashboardViewModel>();
        s.AddSingleton<InstallationViewModel>();
        s.AddSingleton<ServerControlViewModel>();
        s.AddSingleton<ConfigurationViewModel>();
        s.AddSingleton<BackupsViewModel>();
        s.AddSingleton<ModsViewModel>();
        s.AddSingleton<SettingsViewModel>();
    }

    private void OnTrayShowMainWindow(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is { } window)
        {
            if (!window.IsVisible) window.Show();
            if (window.WindowState == Avalonia.Controls.WindowState.Minimized)
                window.WindowState = Avalonia.Controls.WindowState.Normal;
            window.Activate();
        }
    }

    private void OnTrayStartServer(object? sender, EventArgs e)
    {
        try
        {
            var server = Services.GetRequiredService<IServerProcessService>();
            _ = server.StartAsync();
        }
        catch { /* swallow — tray must not crash */ }
    }

    private void OnTrayStopServer(object? sender, EventArgs e)
    {
        try
        {
            var server = Services.GetRequiredService<IServerProcessService>();
            _ = server.StopAsync();
        }
        catch { /* swallow — tray must not crash */ }
    }

    private void OnTrayQuit(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
