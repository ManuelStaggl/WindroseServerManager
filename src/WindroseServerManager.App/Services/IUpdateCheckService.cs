namespace WindroseServerManager.App.Services;

public interface IUpdateCheckService
{
    Task<UpdateCheckResult> CheckAsync(string? installDir, CancellationToken ct = default);
}

public sealed record UpdateCheckResult(
    bool HasUpdate,
    string? InstalledBuildId,
    string? LatestBuildId,
    string? Message);
