using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public interface IWindrosePlusService : IDisposable
{
    /// <summary>Fetch latest WindrosePlus release metadata from GitHub. Returns cached metadata if offline AND cache exists. Throws <see cref="WindrosePlusOfflineException"/> if offline AND no cache.</summary>
    Task<WindrosePlusRelease> FetchLatestAsync(CancellationToken ct = default);

    /// <summary>Install WindrosePlus + UE4SS into the server install dir atomically. Preserves user-owned config files. Writes .wplus-version marker.</summary>
    Task<WindrosePlusInstallResult> InstallAsync(string serverInstallDir, IProgress<InstallProgress>? progress, CancellationToken ct = default);

    /// <summary>Decide which executable to launch for this server. When WindrosePlus is active, returns WindroseServer.exe directly (BuildPak runs via RunPreLaunchAsync). Returns absolute paths only.</summary>
    (string ExePath, string ExtraArgs) ResolveLauncher(string serverInstallDir, ServerInstallInfo info);

    /// <summary>Run WindrosePlus-BuildPak.ps1 before server launch if WindrosePlus is active. No-op if not active or script not found.</summary>
    Task RunPreLaunchAsync(string serverInstallDir, CancellationToken ct = default);

    /// <summary>Read .wplus-version marker from server dir. Returns null if missing or malformed.</summary>
    WindrosePlusVersionMarker? ReadVersionMarker(string serverInstallDir);

    /// <summary>Start the WindrosePlus dashboard server process (windrose_plus_server.ps1). No-op if not active or script not found.</summary>
    Task StartDashboardAsync(string serverInstallDir, CancellationToken ct = default);

    /// <summary>Stop the dashboard server process for this server dir.</summary>
    void StopDashboard(string serverInstallDir);

    /// <summary>
    /// Mirrors {serverDir}/windrose_plus/tools/ to {serverDir}/tools/ so the WindrosePlus
    /// Lua mod can locate generateTiles.ps1 at the path it expects. Idempotent — safe to call
    /// on every launch to retrofit servers that were installed before this workaround existed.
    /// </summary>
    void EnsureRootToolsMirror(string serverInstallDir);
}

/// <summary>Thrown when a fresh install is attempted while offline and no cached archive exists.</summary>
public sealed class WindrosePlusOfflineException : Exception
{
    public WindrosePlusOfflineException(string message) : base(message) { }
    public WindrosePlusOfflineException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Thrown when the downloaded archive's SHA-256 does not match the publisher digest.</summary>
public sealed class WindrosePlusDigestMismatchException : Exception
{
    public WindrosePlusDigestMismatchException(string message) : base(message) { }
}
