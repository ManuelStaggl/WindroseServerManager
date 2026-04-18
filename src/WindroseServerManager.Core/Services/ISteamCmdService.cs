namespace WindroseServerManager.Core.Services;

public interface ISteamCmdService
{
    /// <summary>
    /// Ensures steamcmd.exe is installed and self-updated locally. Returns the absolute path.
    /// </summary>
    Task<string> EnsureSteamCmdAsync(IProgress<string>? log, CancellationToken ct = default);

    /// <summary>
    /// Runs steamcmd.exe with the given arguments and streams stdout+stderr lines as they arrive.
    /// Throws OperationCanceledException on cancellation; raises after process is killed cleanly.
    /// </summary>
    IAsyncEnumerable<string> RunAsync(string arguments, CancellationToken ct = default);
}
