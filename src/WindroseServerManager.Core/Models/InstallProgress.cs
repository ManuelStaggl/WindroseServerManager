namespace WindroseServerManager.Core.Models;

public enum InstallPhase
{
    Preparing,
    DownloadingSteamCmd,
    RunningSteamCmd,
    DownloadingServer,
    Validating,
    Complete,
    Failed,
}

public sealed record InstallProgress(
    InstallPhase Phase,
    string Message,
    double? Percent,
    string? LogLine);
