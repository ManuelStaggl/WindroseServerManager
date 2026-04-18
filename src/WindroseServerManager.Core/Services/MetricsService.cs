using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public sealed class MetricsService : IMetricsService, IDisposable
{
    private readonly ILogger<MetricsService> _logger;
    private readonly IServerProcessService _serverProc;
    private readonly object _cpuLock = new();

    // Host CPU sampling state
    private DateTime _lastHostSampleUtc = DateTime.MinValue;
    private TimeSpan _lastHostCpuTotal = TimeSpan.Zero;
    private double _lastHostCpuPercent;

    // Process CPU sampling state (keyed by PID)
    private int _lastProcPid = -1;
    private DateTime _lastProcSampleUtc = DateTime.MinValue;
    private TimeSpan _lastProcCpuTotal = TimeSpan.Zero;
    private double _lastProcCpuPercent;

    public MetricsService(ILogger<MetricsService> logger, IServerProcessService serverProc)
    {
        _logger = logger;
        _serverProc = serverProc;
    }

    public Task<HostMetrics> GetHostMetricsAsync(string? diskPath = null, CancellationToken ct = default)
    {
        double cpuPercent = SampleHostCpu();
        (long ramUsed, long ramTotal) = GetHostRam();
        (string diskRoot, long diskFree, long diskTotal) = GetDiskInfo(diskPath);

        return Task.FromResult(new HostMetrics(cpuPercent, ramUsed, ramTotal, diskFree, diskTotal, diskRoot));
    }

    public ProcessMetrics? GetServerProcessMetrics()
    {
        var pid = _serverProc.ProcessId;
        if (pid is null) return null;

        try
        {
            using var p = Process.GetProcessById(pid.Value);
            var sampledCpu = SampleProcessCpu(p);
            var uptime = _serverProc.StartedAtUtc is null
                ? TimeSpan.Zero
                : DateTime.UtcNow - _serverProc.StartedAtUtc.Value;
            return new ProcessMetrics(pid.Value, sampledCpu, p.WorkingSet64, uptime);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Cannot read server process metrics (pid={Pid})", pid);
            return null;
        }
    }

    private double SampleHostCpu()
    {
        lock (_cpuLock)
        {
            var now = DateTime.UtcNow;
            var total = TimeSpan.Zero;
            foreach (var p in Process.GetProcesses())
            {
                try { total += p.TotalProcessorTime; } catch { /* access denied, skip */ }
                finally { p.Dispose(); }
            }
            if (_lastHostSampleUtc != DateTime.MinValue)
            {
                var elapsed = (now - _lastHostSampleUtc).TotalMilliseconds;
                var cpuDelta = (total - _lastHostCpuTotal).TotalMilliseconds;
                if (elapsed > 0 && Environment.ProcessorCount > 0)
                {
                    _lastHostCpuPercent = Math.Clamp(
                        cpuDelta / (elapsed * Environment.ProcessorCount) * 100.0,
                        0, 100);
                }
            }
            _lastHostSampleUtc = now;
            _lastHostCpuTotal = total;
            return _lastHostCpuPercent;
        }
    }

    private double SampleProcessCpu(Process p)
    {
        lock (_cpuLock)
        {
            var now = DateTime.UtcNow;
            TimeSpan cpuTotal;
            try { cpuTotal = p.TotalProcessorTime; } catch { return _lastProcCpuPercent; }

            if (_lastProcPid == p.Id && _lastProcSampleUtc != DateTime.MinValue)
            {
                var elapsed = (now - _lastProcSampleUtc).TotalMilliseconds;
                var cpuDelta = (cpuTotal - _lastProcCpuTotal).TotalMilliseconds;
                if (elapsed > 0 && Environment.ProcessorCount > 0)
                {
                    _lastProcCpuPercent = Math.Clamp(
                        cpuDelta / (elapsed * Environment.ProcessorCount) * 100.0,
                        0, 100);
                }
            }
            else
            {
                _lastProcCpuPercent = 0;
            }
            _lastProcPid = p.Id;
            _lastProcSampleUtc = now;
            _lastProcCpuTotal = cpuTotal;
            return _lastProcCpuPercent;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    private (long used, long total) GetHostRam()
    {
        if (!OperatingSystem.IsWindows()) return (0, 0);
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref status)) return (0, 0);
        var total = (long)status.ullTotalPhys;
        var avail = (long)status.ullAvailPhys;
        return (total - avail, total);
    }

    private (string root, long free, long total) GetDiskInfo(string? diskPath)
    {
        try
        {
            string root;
            if (!string.IsNullOrWhiteSpace(diskPath) && Directory.Exists(diskPath))
            {
                root = Path.GetPathRoot(Path.GetFullPath(diskPath)) ?? "C:\\";
            }
            else
            {
                root = Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:\\";
            }
            var di = new DriveInfo(root);
            if (!di.IsReady) return (root, 0, 0);
            return (root, di.AvailableFreeSpace, di.TotalSize);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Cannot get disk info for {Path}", diskPath);
            return (diskPath ?? "?", 0, 0);
        }
    }

    public void Dispose() { /* nothing persistent */ }
}
