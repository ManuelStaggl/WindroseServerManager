using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public interface IServerInstallService
{
    Task<ServerInstallInfo> GetInstallInfoAsync(string installDir, CancellationToken ct = default);

    IAsyncEnumerable<InstallProgress> InstallOrUpdateAsync(
        string installDir,
        CancellationToken ct = default);

    /// <summary>Validates the install directory for writability and sanity. Returns an error message or null if OK.</summary>
    string? ValidateInstallDir(string installDir);
}
