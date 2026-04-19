using System.Net;
using System.Net.Sockets;
using WindroseServerManager.Core.Services;
using Xunit;

namespace WindroseServerManager.Core.Tests.Phase9;

public class FreePortProbeTests
{
    [Fact]
    public void FindFreePort_ReturnsPositivePort()
    {
        var port = FreePortProbe.FindFreePort();
        Assert.InRange(port, 1, 65535);
    }

    [Fact]
    public void FindFreePort_CanBeBoundByCaller_AfterReturn()
    {
        var port = FreePortProbe.FindFreePort();
        // Probe must not hold the port itself — caller should be able to bind immediately.
        var listener = new TcpListener(IPAddress.Loopback, port);
        try
        {
            listener.Start();
            Assert.Equal(port, ((IPEndPoint)listener.LocalEndpoint).Port);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void FindFreePort_WhenPreferredRangeFull_FallsBackOutsideRange()
    {
        const int start = 18080;
        const int end = 18099;
        var occupiers = new List<TcpListener>();
        try
        {
            // Occupy the entire preferred range.
            for (int p = start; p <= end; p++)
            {
                try
                {
                    var l = new TcpListener(IPAddress.Loopback, p);
                    l.Start();
                    occupiers.Add(l);
                }
                catch (SocketException)
                {
                    // Port already in use by something else — still counts as "occupied".
                }
            }

            var port = FreePortProbe.FindFreePort(start, end);
            Assert.True(port > 0);
            Assert.False(port >= start && port <= end,
                $"Expected fallback outside {start}..{end}, got {port}");
        }
        finally
        {
            foreach (var l in occupiers)
            {
                try { l.Stop(); } catch { /* ignore */ }
            }
        }
    }
}
