using System.Text.Json;
using PiggyMetrics.Shared.Hosting;
using PiggyMetrics.StatisticsService.Domain;

namespace PiggyMetrics.StatisticsService.Client;

public sealed class ExchangeRatesClientImpl : ExchangeRatesClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ExchangeRatesClientFallback _fallback;

    public ExchangeRatesClientImpl(IHttpClientFactory httpClientFactory, ExchangeRatesClientFallback fallback)
    {
        _httpClientFactory = httpClientFactory;
        _fallback = fallback;
    }

    public ExchangeRatesContainer GetRates(Currency baseCurrency)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("rates-client");
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "latest?base=" + Uri.EscapeDataString(baseCurrency.ToString()));
            using var response = client.Send(request);
            if (!response.IsSuccessStatusCode)
            {
                return _fallback.GetRates(baseCurrency);
            }

            using var stream = response.Content.ReadAsStream();
            var container = JsonSerializer.Deserialize<ExchangeRatesContainer>(stream, PiggyMetricsJsonOptions.Create());
            if (container is null || container.Rates is null)
            {
                return _fallback.GetRates(baseCurrency);
            }

            return container;
        }
        catch (Exception)
        {
            return _fallback.GetRates(baseCurrency);
        }
    }
}
