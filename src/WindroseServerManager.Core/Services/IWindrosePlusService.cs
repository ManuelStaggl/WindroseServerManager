using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public interface IWindrosePlusService
{
    /// <summary>Fetch latest WindrosePlus release metadata from GitHub. Returns cached metadata if offline AND cache exists. Throws <see cref="WindrosePlusOfflineException"/> if offline AND no cache.</summary>
    Task<WindrosePlusRelease> FetchLatestAsync(CancellationToken ct = default);

    /// <summary>Install WindrosePlus + UE4SS into the server install dir atomically. Preserves user-owned config files. Writes .wplus-version marker.</summary>
    Task<WindrosePlusInstallResult> InstallAsync(string serverInstallDir, IProgress<InstallProgress>? progress, CancellationToken ct = default);

    /// <summary>Decide which executable to launch for this server based on WindrosePlusActive + presence of StartWindrosePlusServer.bat. Returns absolute paths only.</summary>
    (string ExePath, string ExtraArgs) ResolveLauncher(string serverInstallDir, ServerInstallInfo info);

    /// <summary>Read .wplus-version marker from server dir. Returns null if missing or malformed.</summary>
    WindrosePlusVersionMarker? ReadVersionMarker(string serverInstallDir);
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
