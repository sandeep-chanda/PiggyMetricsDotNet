using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PiggyMetrics.AccountService.Domain;
using PiggyMetrics.Shared.Hosting;
using PiggyMetrics.Shared.Http;

namespace PiggyMetrics.AccountService.Client;

public sealed class AuthServiceClientImpl : AuthServiceClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ClientCredentialsTokenCache _tokenCache;
    private readonly IOptions<ClientCredentialsOptions> _credentials;

    public AuthServiceClientImpl(
        IHttpClientFactory httpClientFactory,
        ClientCredentialsTokenCache tokenCache,
        IOptions<ClientCredentialsOptions> credentials)
    {
        _httpClientFactory = httpClientFactory;
        _tokenCache = tokenCache;
        _credentials = credentials;
    }

    public void CreateUser(User user)
    {
        var client = _httpClientFactory.CreateClient("auth-service");
        using var request = new HttpRequestMessage(HttpMethod.Post, "uaa/users")
        {
            Content = JsonContent.Create(user, options: PiggyMetricsJsonOptions.Create())
        };
        var token = _tokenCache.GetAccessTokenAsync(_credentials.Value, CancellationToken.None).GetAwaiter().GetResult();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = client.Send(request);
        response.EnsureSuccessStatusCode();
    }
}
