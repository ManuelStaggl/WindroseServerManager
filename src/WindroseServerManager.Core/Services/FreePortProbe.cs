using System.Net;
using System.Net.Sockets;

namespace WindroseServerManager.Core.Services;

/// <summary>
/// Findet einen freien TCP-Port für den WindrosePlus-Dashboard-Server.
/// Probiert zuerst den bevorzugten Range (18080..18099), fällt sonst auf einen OS-zugewiesenen ephemeren Port zurück.
/// </summary>
public static class FreePortProbe
{
    /// <summary>Gibt einen Port zurück der im Moment der Prüfung frei war. Kein Hold — Caller muss sofort binden.</summary>
    public static int FindFreePort(int rangeStart = 18080, int rangeEnd = 18099)
    {
        for (int port = rangeStart; port <= rangeEnd; port++)
        {
            try
            {
                var l = new TcpListener(IPAddress.Loopback, port);
                l.Start();
                l.Stop();
                return port;
            }
            catch (SocketException)
            {
                // in use, try next
            }
        }

        // Fallback: OS-assigned ephemeral
        var fallback = new TcpListener(IPAddress.Loopback, 0);
        fallback.Start();
        int assigned = ((IPEndPoint)fallback.LocalEndpoint).Port;
        fallback.Stop();
        return assigned;
    }
}
