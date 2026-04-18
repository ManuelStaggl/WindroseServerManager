namespace WindroseServerManager.Core.Models;

public sealed record HostMetrics(
    double CpuPercent,
    long RamUsedBytes,
    long RamTotalBytes,
    long DiskFreeBytes,
    long DiskTotalBytes,
    string DiskRoot);

public sealed record ProcessMetrics(
    int ProcessId,
    double CpuPercent,
    long RamBytes,
    TimeSpan Uptime);
