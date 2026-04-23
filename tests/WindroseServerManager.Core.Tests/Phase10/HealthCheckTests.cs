using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WindroseServerManager.Core.Services;
using Xunit;

namespace WindroseServerManager.Core.Tests.Phase10;

public class HealthCheckTests
{
    // Captures the last request URI and returns a caller-supplied response.
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;

        public Uri? LastRequestUri { get; private set; }

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> factory)
            => _factory = factory;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(_factory(request));
        }
    }

    // Awaits a long delay so the linked CTS fires before the request "completes".
    private sealed class DelayHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    // Handler that throws if called — proves no HTTP call was made.
    private sealed class ThrowIfCalledHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("HTTP call should not have been made for port=0.");
    }

    [Fact]
    public async Task IsHealthyAsync_PortZero_ReturnsFalseWithoutHttpCall()
    {
        var handler = new ThrowIfCalledHandler();
        using var client = new HttpClient(handler);

        var result = await HealthCheckHelper.IsHealthyAsync(0, client, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task IsHealthyAsync_Timeout_ReturnsFalse()
    {
        using var handler = new DelayHandler();
        using var client = new HttpClient(handler);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        var result = await HealthCheckHelper.IsHealthyAsync(7777, client, cts.Token);

        Assert.False(result);
    }

    [Fact]
    public async Task IsHealthyAsync_200Response_ReturnsTrue()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(handler);

        const int port = 7777;
        var result = await HealthCheckHelper.IsHealthyAsync(port, client, CancellationToken.None);

        Assert.True(result);
        Assert.Equal($"http://localhost:{port}/api/health", handler.LastRequestUri!.ToString());
    }
}
