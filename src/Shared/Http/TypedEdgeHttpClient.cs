using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PiggyMetrics.Shared.Http;

public static class EdgeHttpDefaults
{
    public static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(10000);
}

public sealed class TypedEdgeHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ClientCredentialsTokenCache _tokenCache;
    private readonly ClientCredentialsOptions _credentials;

    public TypedEdgeHttpClient(
        HttpClient httpClient,
        ClientCredentialsTokenCache tokenCache,
        IOptions<ClientCredentialsOptions> credentials)
    {
        _httpClient = httpClient;
        _tokenCache = tokenCache;
        _credentials = credentials.Value;
        _httpClient.Timeout = EdgeHttpDefaults.Timeout;
    }

    public HttpClient Http => _httpClient;

    public async Task<HttpResponseMessage> SendWithClientCredentialsAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _tokenCache.GetAccessTokenAsync(_credentials, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _httpClient.SendAsync(request, cancellationToken);
    }
}

public static class TypedEdgeHttpClientExtensions
{
    public static IServiceCollection AddTypedEdgeHttpClient(
        this IServiceCollection services,
        string edgeName,
        Uri baseAddress)
    {
        services.AddSingleton<ClientCredentialsTokenCache>();
        services.AddHttpClient(edgeName, client =>
        {
            client.BaseAddress = baseAddress;
            client.Timeout = EdgeHttpDefaults.Timeout;
        });
        return services;
    }
}
