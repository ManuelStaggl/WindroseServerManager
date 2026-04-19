using System.Net;
using System.Net.Http;

namespace WindroseServerManager.Core.Tests.TestDoubles;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;
    public List<HttpRequestMessage> Requests { get; } = new();

    public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => _send = send;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return await _send(request, cancellationToken).ConfigureAwait(false);
    }

    public static FakeHttpMessageHandler ThrowsOffline() =>
        new((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("Offline (fake)")));
}
