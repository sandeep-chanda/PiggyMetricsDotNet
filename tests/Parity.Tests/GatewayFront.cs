extern alias GatewayApp;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace PiggyMetrics.Parity.Tests;

/// <summary>
/// Real gateway in front of an in-process service. Replay clients talk only to this host.
/// </summary>
public sealed class GatewayFront : IAsyncDisposable
{
    private readonly WebApplicationFactory<GatewayApp::Program> _factory;

    private GatewayFront(WebApplicationFactory<GatewayApp::Program> factory, HttpClient client, List<string> forwarded)
    {
        _factory = factory;
        Client = client;
        Forwarded = forwarded;
    }

    public HttpClient Client { get; }

    public IReadOnlyList<string> Forwarded { get; }

    public static GatewayFront Start(HttpClient downstream)
    {
        var forwarded = new List<string>();
        var factory = new WebApplicationFactory<GatewayApp::Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.Configure<HttpClientFactoryOptions>("zuul", options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                    {
                        handlerBuilder.PrimaryHandler = new RelayHandler(downstream, forwarded);
                    });
                });
            });
        });

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        return new GatewayFront(factory, client, forwarded);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
    }

    private sealed class RelayHandler : HttpMessageHandler
    {
        private readonly HttpClient _downstream;
        private readonly List<string> _forwarded;

        public RelayHandler(HttpClient downstream, List<string> forwarded)
        {
            _downstream = downstream;
            _forwarded = forwarded;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? "/";
            if (!path.StartsWith('/'))
            {
                path = "/" + path;
            }

            _forwarded.Add(request.Method.Method + " " + request.RequestUri?.AbsolutePath);
            using var relay = new HttpRequestMessage(request.Method, path);
            foreach (var header in request.Headers)
            {
                if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                relay.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (request.Content is not null)
            {
                var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                var content = new ByteArrayContent(bytes);
                foreach (var header in request.Content.Headers)
                {
                    content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                relay.Content = content;
            }

            return await _downstream.SendAsync(relay, cancellationToken);
        }
    }
}
