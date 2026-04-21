using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public interface IServerProcessService
{
    ServerStatus Status { get; }
    DateTime? StartedAtUtc { get; }
    int? ProcessId { get; }
    int? LastExitCode { get; }

    /// <summary>Snapshot of the most recent log lines (newest at end).</summary>
    IReadOnlyList<ServerLogLine> RecentLog { get; }

    event Action<ServerStatus>? StatusChanged;
    event Action<ServerLogLine>? LogAppended;

    /// <summary>Starts the Windrose dedicated server. No-op if already running.</summary>
    Task<bool> StartAsync(CancellationToken ct = default);

    /// <summary>Tries a graceful stop (CloseMainWindow + wait GracefulShutdownSeconds). Falls back to Kill.</summary>
    Task StopAsync(CancellationToken ct = default);

    /// <summary>Immediately kills the process tree. No graceful shutdown.</summary>
    Task KillAsync(CancellationToken ct = default);

    /// <summary>Validates that a server binary exists at the configured install dir. Returns error message or null.</summary>
    string? ValidateCanStart();

    /// <summary>
    /// Scans for an already-running Windrose server process in the active install dir and attaches to it.
    /// Returns true if a process was found and attached, false otherwise.
    /// </summary>
    bool TryAttachToExistingProcess();
}
