namespace WindroseServerManager.Core.Services;

public interface ISteamCmdService
{
    /// <summary>
    /// Ensures steamcmd.exe is installed and self-updated locally.
    /// Streams log lines in real time and returns the absolute path as the final element.
    /// </summary>
    IAsyncEnumerable<string> EnsureSteamCmdAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs steamcmd.exe with the given arguments and streams stdout+stderr lines as they arrive.
    /// Throws OperationCanceledException on cancellation; raises after process is killed cleanly.
    /// </summary>
    IAsyncEnumerable<string> RunAsync(string arguments, CancellationToken ct = default);
}
