using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Serilog;
using WindroseServerManager.App.Services;
using WindroseServerManager.App.ViewModels;
using WindroseServerManager.App.Views;
using WindroseServerManager.Core.Models;
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

        // Load settings synchronously — safe here because the Avalonia dispatcher loop
        // hasn't started yet, so there's no sync context to cause a deadlock.
        // This ensures settings.Current is populated before ViewModels read it.
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

            // Async shutdown — no deadlock risk here because the dispatcher is still
            // running but we use await (not .Result) which cooperates with it.
            desktop.ShutdownRequested += async (_, args) =>
            {
                try { if (_host is not null) await _host.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
                catch { }
            };
        }

        // Start background services (RestartScheduler, DiscordBot, etc.)
        _ = _host.StartAsync();

        // Auto-start eligible servers. Eligibility rule:
        //   effective = AppSettings.AutoStartServerOnAppLaunch  OR  ServerEntry.AutoStartOnAppLaunch
        // The active server is started via IServerProcessService so it's fully tracked
        // (status, logs, event log). All OTHER eligible servers are launched via
        // Process.Start on their StartWindrosePlusServer.bat — they run detached and the
        // admin can switch to them later via "Set Active" on the card.
        _ = AutoStartEligibleServersAsync(settings);

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task AutoStartEligibleServersAsync(IAppSettingsService settings)
    {
        try
        {
            // Grace period so the main window is mounted before launching.
            await Task.Delay(TimeSpan.FromMilliseconds(500));

            var global = settings.Current.AutoStartServerOnAppLaunch;
            var activeId = settings.Current.ActiveServerId;
            var eligible = settings.Current.Servers
                .Where(s => global || s.AutoStartOnAppLaunch)
                .ToList();

            if (eligible.Count == 0)
            {
                Log.Debug("Auto-start: no eligible servers");
                return;
            }

            Log.Information("Auto-start: {Count} eligible server(s)", eligible.Count);

            foreach (var entry in eligible)
            {
                if (entry.Id == activeId)
                {
                    await TryStartActiveServerAsync(entry);
                }
                else
                {
                    TryStartNonActiveServer(entry);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Auto-start loop failed");
        }
    }

    private static async Task TryStartActiveServerAsync(Core.Models.ServerEntry entry)
    {
        try
        {
            var server = Services.GetRequiredService<IServerProcessService>();
            if (server.Status is ServerStatus.Running or ServerStatus.Starting)
            {
                Log.Information("Auto-start: active server '{Name}' already running — skip", entry.Name);
                return;
            }
            Log.Information("Auto-start: launching active server '{Name}'", entry.Name);
            await server.StartAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Auto-start: failed to start active server '{Name}'", entry.Name);
        }
    }

    private static void TryStartNonActiveServer(Core.Models.ServerEntry entry)
    {
        try
        {
            // Non-active servers are launched via their StartWindrosePlusServer.bat if present
            // (handles WindrosePlus build/pre-launch), else the raw server binary.
            var launchBat = System.IO.Path.Combine(entry.InstallDir, "StartWindrosePlusServer.bat");
            string? target = System.IO.File.Exists(launchBat)
                ? launchBat
                : Core.Services.ServerInstallService.FindServerBinary(entry.InstallDir);

            if (target is null)
            {
                Log.Warning("Auto-start: no launchable file found for '{Name}' — skip", entry.Name);
                return;
            }

            // Naive duplicate-check: skip if a WindroseServer-Win64-Shipping.exe process
            // already runs out of this install dir (best-effort).
            if (IsServerRunningFromDir(entry.InstallDir))
            {
                Log.Information("Auto-start: server '{Name}' already running in {Dir} — skip", entry.Name, entry.InstallDir);
                return;
            }

            Log.Information("Auto-start: launching non-active server '{Name}' via {Target}", entry.Name, target);
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = target,
                WorkingDirectory = entry.InstallDir,
                UseShellExecute = true,
            };
            // WP_NOPAUSE tells the bat wrapper not to "pause" on errors — we're headless.
            psi.EnvironmentVariables["WP_NOPAUSE"] = "1";
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Auto-start: failed to start non-active server '{Name}'", entry.Name);
        }
    }

    private static bool IsServerRunningFromDir(string installDir)
    {
        try
        {
            var normalized = System.IO.Path.GetFullPath(installDir).TrimEnd('\\', '/');
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("WindroseServer-Win64-Shipping"))
            {
                try
                {
                    var main = p.MainModule?.FileName;
                    if (main is not null &&
                        main.StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch { /* access-denied on some processes — ignore */ }
                finally { p.Dispose(); }
            }
        }
        catch (Exception ex) { Log.Debug(ex, "IsServerRunningFromDir probe failed"); }
        return false;
    }

    private static void ConfigureServices(IServiceCollection s)
    {
        s.AddLogging(b => b.ClearProviders().AddSerilog(Log.Logger));
        s.AddHttpClient();

        s.AddSingleton<IAppSettingsService, AppSettingsService>();
        s.AddSingleton<ILocalizationService, LocalizationService>();
        s.AddSingleton<ISteamCmdService, SteamCmdService>();
        s.AddSingleton<IWindrosePlusService, WindrosePlusService>();
        s.AddSingleton<IWindrosePlusApiService, WindrosePlusApiService>();
        s.AddSingleton<IServerInstallService, ServerInstallService>();
        s.AddSingleton<IServerProcessService, ServerProcessService>();
        s.AddSingleton<IServerConfigService, ServerConfigService>();
        s.AddSingleton<IBackupService, BackupService>();
        s.AddSingleton<IModService, ModService>();
        s.AddSingleton<IMetricsService, MetricsService>();
        s.AddSingleton<IServerEventLog, ServerEventLog>();

        // Discord Bot Services
        s.AddSingleton<DiscordSocketClient>(sp => {
            return new DiscordSocketClient(new DiscordSocketConfig {
                GatewayIntents = GatewayIntents.Guilds,
                LogLevel = LogSeverity.Info
            });
        });
        s.AddSingleton<InteractionService>(sp => new InteractionService(sp.GetRequiredService<DiscordSocketClient>()));
        s.AddSingleton<ServerCommandModule>(); // Important: Register the module itself

        s.AddHostedService<BackupScheduler>();
        s.AddSingleton<RestartScheduler>();
        s.AddHostedService(sp => sp.GetRequiredService<RestartScheduler>());

        s.AddSingleton<INavigationService, NavigationService>();
        s.AddSingleton<IToastService, ToastService>();
        s.AddSingleton<INotificationService>(sp => 
            new NotificationServiceAdapter(sp.GetRequiredService<IToastService>()));
        s.AddSingleton<IFirewallService, FirewallService>();
        s.AddSingleton<IUpdateCheckService, UpdateCheckService>();
        s.AddSingleton<IAutoStartService, AutoStartService>();
        s.AddSingleton<IAppUpdateService, AppUpdateService>();
        s.AddHostedService<AppUpdateScheduler>();
        s.AddSingleton<IWindrosePlusUpdateService, WindrosePlusUpdateService>();
        s.AddHostedService<WindrosePlusUpdateScheduler>();
        s.AddHostedService<DiscordBotService>();

        s.AddSingleton<MainWindowViewModel>();

        // (Tray icon handlers below.)
        s.AddSingleton<DashboardViewModel>();
        s.AddSingleton<InstallationViewModel>();
        s.AddTransient<InstallWizardViewModel>();
        s.AddSingleton<ServerControlViewModel>();
        s.AddSingleton<ConfigurationViewModel>();
        s.AddSingleton<BackupsViewModel>();
        s.AddSingleton<ModsViewModel>();
        s.AddSingleton<SettingsViewModel>();

        // Phase 11 — Feature Views (WindrosePlus)
        s.AddSingleton<PlayersViewModel>();
        s.AddSingleton<EventsViewModel>();
        s.AddSingleton<EditorViewModel>();
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
