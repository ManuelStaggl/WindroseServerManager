using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public interface IMetricsService
{
    Task<HostMetrics> GetHostMetricsAsync(string? diskPath = null, CancellationToken ct = default);

    /// <summary>Returns null when no server process is running.</summary>
    ProcessMetrics? GetServerProcessMetrics();
}
