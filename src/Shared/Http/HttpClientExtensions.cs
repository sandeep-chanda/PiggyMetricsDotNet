using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PiggyMetrics.Shared.Http;

public static class HttpClientExtensions
{
	public static IServiceCollection AddPiggyMetricsClientCredentials(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		services.Configure<ServiceClientOptions>(configuration.GetSection(ServiceClientOptions.SectionName));
		services.AddHttpClient(ClientCredentialsTokenProvider.TokenHttpClientName, client =>
		{
			client.Timeout = TimeSpan.FromMilliseconds(ServiceClientOptions.SourceTimeoutMilliseconds);
		});
		services.AddSingleton<IClientCredentialsTokenProvider, ClientCredentialsTokenProvider>();
		services.AddTransient<ClientCredentialsHandler>();
		return services;
	}

	// One HTTP client per service-to-service edge, carrying the client-credentials token.
	public static IHttpClientBuilder AddPiggyMetricsServiceClient<TClient>(
		this IServiceCollection services,
		string name,
		Uri baseAddress)
		where TClient : class
	{
		return services
			.AddPiggyMetricsEdgeClient<TClient>(name, baseAddress)
			.AddHttpMessageHandler<ClientCredentialsHandler>();
	}

	// One HTTP client per edge that the source calls without a token.
	public static IHttpClientBuilder AddPiggyMetricsEdgeClient<TClient>(
		this IServiceCollection services,
		string name,
		Uri baseAddress)
		where TClient : class
	{
		return services.AddHttpClient<TClient>(name, client =>
		{
			client.BaseAddress = baseAddress;
			client.Timeout = TimeSpan.FromMilliseconds(ServiceClientOptions.SourceTimeoutMilliseconds);
		});
	}
}
