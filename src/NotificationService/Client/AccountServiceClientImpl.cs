using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using PiggyMetrics.Shared.Http;

namespace PiggyMetrics.NotificationService.Client;

public sealed class AccountServiceClientImpl : AccountServiceClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ClientCredentialsTokenCache _tokenCache;
    private readonly IOptions<ClientCredentialsOptions> _credentials;

    public AccountServiceClientImpl(
        IHttpClientFactory httpClientFactory,
        ClientCredentialsTokenCache tokenCache,
        IOptions<ClientCredentialsOptions> credentials)
    {
        _httpClientFactory = httpClientFactory;
        _tokenCache = tokenCache;
        _credentials = credentials;
    }

    public string GetAccount(string accountName)
    {
        var client = _httpClientFactory.CreateClient("account-service");
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "accounts/" + Uri.EscapeDataString(accountName));
        var token = _tokenCache.GetAccessTokenAsync(_credentials.Value, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = client.Send(request);
        response.EnsureSuccessStatusCode();
        return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
    }
}
