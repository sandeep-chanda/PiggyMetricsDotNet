using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PiggyMetrics.AccountService.Domain;
using PiggyMetrics.Shared.Hosting;
using PiggyMetrics.Shared.Http;

namespace PiggyMetrics.AccountService.Client;

public sealed class StatisticsServiceClientImpl : StatisticsServiceClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ClientCredentialsTokenCache _tokenCache;
    private readonly IOptions<ClientCredentialsOptions> _credentials;
    private readonly StatisticsServiceClientFallback _fallback;

    public StatisticsServiceClientImpl(
        IHttpClientFactory httpClientFactory,
        ClientCredentialsTokenCache tokenCache,
        IOptions<ClientCredentialsOptions> credentials,
        StatisticsServiceClientFallback fallback)
    {
        _httpClientFactory = httpClientFactory;
        _tokenCache = tokenCache;
        _credentials = credentials;
        _fallback = fallback;
    }

    public void UpdateStatistics(string accountName, Account account)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("statistics-service");
            using var request = new HttpRequestMessage(
                HttpMethod.Put,
                "statistics/" + Uri.EscapeDataString(accountName))
            {
                Content = JsonContent.Create(account, options: PiggyMetricsJsonOptions.Create())
            };
            var token = _tokenCache.GetAccessTokenAsync(_credentials.Value, CancellationToken.None).GetAwaiter().GetResult();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = client.Send(request);
            if (!response.IsSuccessStatusCode)
            {
                _fallback.UpdateStatistics(accountName, account);
            }
        }
        catch (Exception)
        {
            _fallback.UpdateStatistics(accountName, account);
        }
    }
}
