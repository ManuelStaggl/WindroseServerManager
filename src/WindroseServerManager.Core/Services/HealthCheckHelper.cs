using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WindroseServerManager.Core.Services;

public static class HealthCheckHelper
{
    private static readonly TimeSpan InternalTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Sends a GET request to http://localhost:{port}/api/health.
    /// Returns true only on a 2xx response.
    /// Returns false when: port &lt;= 0, HttpRequestException, TaskCanceledException,
    /// OperationCanceledException, or a non-success status code.
    /// Uses a linked CancellationTokenSource with a 3-second safety-net in addition
    /// to the caller-supplied token.
    /// </summary>
    public static async Task<bool> IsHealthyAsync(int port, HttpClient httpClient, CancellationToken ct)
    {
        if (port <= 0) return false;
        if (httpClient is null) return false;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(InternalTimeout);

        try
        {
            // /api/health requires no auth — correct endpoint for monitoring.
            var url = $"http://localhost:{port}/api/health";
            using var resp = await httpClient.GetAsync(url, cts.Token).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) { return false; }
        catch (OperationCanceledException) { return false; }
    }
}
