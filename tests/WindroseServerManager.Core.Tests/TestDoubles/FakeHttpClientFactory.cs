using System.Net.Http;

namespace WindroseServerManager.Core.Tests.TestDoubles;

public sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;
    public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
}
