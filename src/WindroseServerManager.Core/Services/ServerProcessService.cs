using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public sealed class ServerProcessService : IServerProcessService, IAsyncDisposable
{
    private const int MaxLogBufferLines = 5000;

    private readonly ILogger<ServerProcessService> _logger;
    private readonly IAppSettingsService _settings;
    private readonly IServerEventLog _events;
    private readonly IWindrosePlusService _windrosePlus;
    private readonly object _lock = new();

    private readonly ConcurrentQueue<ServerLogLine> _logBuffer = new();
    private Process? _process;
    private CancellationTokenSource? _monitorCts;

    public ServerProcessService(
        ILogger<ServerProcessService> logger,
        IAppSettingsService settings,
        IServerEventLog events,
        IWindrosePlusService windrosePlus)
    {
        _logger = logger;
        _settings = settings;
        _events = events;
        _windrosePlus = windrosePlus;
    }

    public ServerStatus Status { get; private set; } = ServerStatus.Stopped;
    public DateTime? StartedAtUtc { get; private set; }
    public int? ProcessId { get; private set; }
    public int? LastExitCode { get; private set; }

    public IReadOnlyList<ServerLogLine> RecentLog => _logBuffer.ToArray();

    public event Action<ServerStatus>? StatusChanged;
    public event Action<ServerLogLine>? LogAppended;

    public string? ValidateCanStart()
    {
        var dir = _settings.Current.ServerInstallDir;
        if (string.IsNullOrWhiteSpace(dir))
            return "Server-Installationspfad ist nicht gesetzt. Erst auf der Installationsseite installieren.";
        try
        {
            var info = BuildInstallInfo(dir);
            var (_, _) = _windrosePlus.ResolveLauncher(dir, info);
            return null;
        }
        catch (FileNotFoundException)
        {
            return $"Server-Binary nicht gefunden in {dir}. Server zuerst installieren.";
        }
    }

    public Task<bool> StartAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (Status is ServerStatus.Running or ServerStatus.Starting)
            {
                _logger.LogDebug("Start requested but server is already {Status}", Status);
                return Task.FromResult(false);
            }

            var err = ValidateCanStart();
            if (err is not null)
            {
                AppendSystem($"[FEHLER] {err}");
                return Task.FromResult(false);
            }

            var dir = _settings.Current.ServerInstallDir;
            var info = BuildInstallInfo(dir);
            var (exe, wplusArgs) = _windrosePlus.ResolveLauncher(dir, info);
            var args = CombineArgs(BuildLaunchArgs(_settings.Current), wplusArgs);

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = Path.GetDirectoryName(exe)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
            };

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null) Append(new ServerLogLine(DateTime.UtcNow, LogStream.Stdout, e.Data));
            };
            _process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null) Append(new ServerLogLine(DateTime.UtcNow, LogStream.Stderr, e.Data));
            };
            _process.Exited += OnExited;

            AppendSystem($"=== Start: {exe} {args}");
            try
            {
                if (!_process.Start())
                {
                    AppendSystem("[FEHLER] Prozess konnte nicht gestartet werden.");
                    CleanupProcess();
                    TransitionTo(ServerStatus.Stopped);
                    return Task.FromResult(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start server process");
                AppendSystem($"[FEHLER] {ex.Message}");
                CleanupProcess();
                TransitionTo(ServerStatus.Stopped);
                return Task.FromResult(false);
            }

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            ProcessId = _process.Id;
            StartedAtUtc = DateTime.UtcNow;
            LastExitCode = null;
            TransitionTo(ServerStatus.Starting);

            // Flip Starting → Running after 3s grace period (Windrose server needs boot time).
            _monitorCts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(3000, _monitorCts.Token).ConfigureAwait(false);
                    lock (_lock)
                    {
                        if (Status == ServerStatus.Starting && _process is { HasExited: false })
                        {
                            TransitionTo(ServerStatus.Running);
                        }
                    }
                }
                catch (OperationCanceledException) { /* ignore */ }
            }, _monitorCts.Token);

            _logger.LogInformation("Started Windrose server pid={Pid} exe={Exe}", ProcessId, exe);

            _ = _events.AppendAsync(new ServerEvent(DateTime.UtcNow, ServerEventType.Started, $"Start via App (pid={ProcessId})"));

            return Task.FromResult(true);
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        Process? p;
        int graceSeconds;
        lock (_lock)
        {
            if (Status is ServerStatus.Stopped or ServerStatus.Stopping) return;
            if (_process is null) return;
            p = _process;
            graceSeconds = Math.Max(5, _settings.Current.GracefulShutdownSeconds);
            TransitionTo(ServerStatus.Stopping);
        }

        AppendSystem($"=== Stop (Windrose kennt keinen Soft-Shutdown — Prozess wird in {graceSeconds}s beendet)");
        try
        {
            if (!p.HasExited)
            {
                // Kurze Karenz: falls der Prozess von selbst endet, sparen wir uns den Kill.
                try
                {
                    using var graceCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    graceCts.CancelAfter(TimeSpan.FromSeconds(graceSeconds));
                    await p.WaitForExitAsync(graceCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { /* erwartet → Kill */ }
            }

            // Nach dem Warte-Fenster: entweder ist der Prozess von selbst beendet (OnExited hat
            // aufgeräumt) oder wir müssen killen. Windrose kennt offiziell keinen graceful shutdown,
            // daher ist Kill der dokumentierte Weg (siehe Community-Guide).
            bool stillAlive;
            try { stillAlive = !p.HasExited; }
            catch { stillAlive = false; /* disposed = beendet */ }

            if (stillAlive)
            {
                try
                {
                    p.Kill(entireProcessTree: true);
                    await p.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                    AppendSystem("Prozess beendet (Kill).");
                }
                catch (InvalidOperationException)
                {
                    // Race: OnExited lief gerade und hat disposed — alles gut.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Kill failed");
                    AppendSystem($"[FEHLER] Kill fehlgeschlagen: {ex.Message}");
                }
            }
        }
        finally
        {
            // Exited event fires OnExited → cleanup.
            KillOrphanServerProcesses();
        }
    }

    public async Task KillAsync(CancellationToken ct = default)
    {
        Process? p;
        lock (_lock)
        {
            if (_process is null || Status == ServerStatus.Stopped) return;
            p = _process;
            TransitionTo(ServerStatus.Stopping);
        }

        AppendSystem("=== Hart-Kill angefordert");
        try
        {
            if (!p.HasExited)
            {
                p.Kill(entireProcessTree: true);
                await p.WaitForExitAsync(ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kill failed");
            AppendSystem($"[FEHLER] Kill fehlgeschlagen: {ex.Message}");
        }
        finally
        {
            KillOrphanServerProcesses();
        }
    }

    private void KillOrphanServerProcesses()
    {
        var installDir = _settings.Current.ServerInstallDir;
        if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir)) return;

        // Sowohl Bootstrap (WindroseServer.exe) als auch echten UE5-Prozess (WindroseServer-Win64-Shipping.exe)
        // verfolgen — letzterer läuft typischerweise in R5\Binaries\Win64\ und ist nach dem Bootstrap-Kill
        // oft noch am Leben (das "cmd-Fenster" des Servers).
        string[] names = { "WindroseServer", "WindroseServer-Win64-Shipping" };

        foreach (var name in names)
        {
            foreach (var proc in Process.GetProcessesByName(name))
            {
                try
                {
                    string? procPath = null;
                    try { procPath = proc.MainModule?.FileName; } catch { /* access denied */ }

                    // Wenn wir den Pfad lesen können: nur Prozesse aus unserem Install-Dir töten.
                    if (procPath is not null
                        && !procPath.StartsWith(Path.GetFullPath(installDir), StringComparison.OrdinalIgnoreCase))
                    {
                        proc.Dispose();
                        continue;
                    }

                    _logger.LogWarning("Killing orphan server process pid={Pid} name={Name} path={Path}", proc.Id, name, procPath);
                    AppendSystem($"Orphan-Prozess beendet ({name}, pid={proc.Id}).");
                    proc.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to kill orphan pid={Pid}", proc.Id);
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
    }

    private void OnExited(object? sender, EventArgs e)
    {
        int? code = null;
        ServerStatus previous;
        DateTime? startedAt;
        lock (_lock)
        {
            previous = Status;
            startedAt = StartedAtUtc;
            try { code = _process?.ExitCode; } catch { /* ignore */ }
            LastExitCode = code;
            CleanupProcess();

            // Determine crashed vs clean stop
            var expected = previous == ServerStatus.Stopping;
            var newStatus = expected ? ServerStatus.Stopped :
                            code == 0 ? ServerStatus.Stopped : ServerStatus.Crashed;
            TransitionTo(newStatus);
        }

        AppendSystem($"=== Prozess beendet. ExitCode={code?.ToString() ?? "?"}");
        _logger.LogInformation("Server exited: code={Code} previousStatus={Prev}", code, previous);

        var sessionDuration = startedAt is null ? (TimeSpan?)null : DateTime.UtcNow - startedAt.Value;
        var crashed = previous != ServerStatus.Stopping && code != 0;
        var evtType = crashed ? ServerEventType.Crashed : ServerEventType.Stopped;
        var reason = crashed
            ? $"Crash (ExitCode={code?.ToString() ?? "?"})"
            : $"Stop (ExitCode={code?.ToString() ?? "?"})";
        _ = _events.AppendAsync(new ServerEvent(DateTime.UtcNow, evtType, reason, code, sessionDuration));

        // Auto-restart on crash if enabled and we weren't the one who stopped it
        if (previous != ServerStatus.Stopping && _settings.Current.AutoRestartOnCrash)
        {
            AppendSystem("Auto-Restart aktiv, starte in 5s neu...");
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(5000).ConfigureAwait(false);
                    await StartAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto-restart failed");
                    AppendSystem($"[FEHLER] Auto-Restart fehlgeschlagen: {ex.Message}");
                }
            });
        }
    }

    private void CleanupProcess()
    {
        try { _monitorCts?.Cancel(); } catch { /* ignore */ }
        _monitorCts?.Dispose();
        _monitorCts = null;

        if (_process is not null)
        {
            _process.Exited -= OnExited;
            try { _process.Dispose(); } catch { /* ignore */ }
            _process = null;
        }
        ProcessId = null;
        StartedAtUtc = null;
    }

    private void Append(ServerLogLine line)
    {
        _logBuffer.Enqueue(line);
        while (_logBuffer.Count > MaxLogBufferLines && _logBuffer.TryDequeue(out _)) { }
        try { LogAppended?.Invoke(line); } catch (Exception ex) { _logger.LogDebug(ex, "LogAppended handler threw"); }
    }

    private void AppendSystem(string text) => Append(ServerLogLine.System(text));

    private void TransitionTo(ServerStatus next)
    {
        if (Status == next) return;
        Status = next;
        try { StatusChanged?.Invoke(next); } catch (Exception ex) { _logger.LogDebug(ex, "StatusChanged handler threw"); }
    }

    private ServerInstallInfo BuildInstallInfo(string dir)
    {
        var s = _settings.Current;
        var key = string.IsNullOrWhiteSpace(dir) ? "" : Path.GetFullPath(dir);
        var active = s.WindrosePlusActiveByServer.TryGetValue(key, out var a) && a;
        var tag = s.WindrosePlusVersionByServer.TryGetValue(key, out var t) ? t : null;
        return new ServerInstallInfo(
            IsInstalled: !string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir),
            InstallDir: dir,
            BuildId: null,
            SizeBytes: 0,
            LastUpdatedUtc: null,
            WindrosePlusActive: active,
            WindrosePlusVersionTag: tag);
    }

    private static string CombineArgs(string a, string b) =>
        string.IsNullOrWhiteSpace(b) ? a : (string.IsNullOrWhiteSpace(a) ? b : a + " " + b);

    private static string BuildLaunchArgs(AppSettings s)
    {
        var parts = new List<string>();
        if (s.LogEnabled) parts.Add("-log");
        if (!string.IsNullOrWhiteSpace(s.ExtraLaunchArgs))
            parts.Add(s.ExtraLaunchArgs.Trim());
        return string.Join(' ', parts);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await KillAsync().ConfigureAwait(false);
        }
        catch { /* swallow */ }
    }
}
