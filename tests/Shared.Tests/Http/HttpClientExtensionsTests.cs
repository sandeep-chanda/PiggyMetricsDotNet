using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PiggyMetrics.Shared.Http;
using Xunit;

namespace PiggyMetrics.Shared.Tests.Http;

// P1.T4 - one HTTP client per edge, each carrying the source's call timeout; the service-to-service
// registration carries the client-credentials token, the edge registration does not.
public sealed class HttpClientExtensionsTests
{
	private static readonly Uri EdgeAddress = new("http://statistics-service:7000");

	[Fact]
	public void ShouldGiveTheTokenClientTheSourcesTimeout()
	{
		using var provider = ClientCredentialsTokenProviderTests.BuildProvider(new TokenEndpointStub(3600));

		var client = provider.GetRequiredService<IHttpClientFactory>()
			.CreateClient(ClientCredentialsTokenProvider.TokenHttpClientName);

		Assert.Equal(TimeSpan.FromMilliseconds(10000), client.Timeout);
		Assert.Equal("piggymetrics-token", ClientCredentialsTokenProvider.TokenHttpClientName);
	}

	[Fact]
	public void ShouldBindTheSourcesClientConfigurationKeys()
	{
		using var provider = ClientCredentialsTokenProviderTests.BuildProvider(new TokenEndpointStub(3600));

		var options = provider.GetRequiredService<IOptions<ServiceClientOptions>>().Value;

		Assert.Equal("account-service", options.ClientId);
		Assert.Equal("secret", options.ClientSecret);
	}

	[Fact]
	public void ShouldCacheOneTokenProviderForTheWholeProcess()
	{
		using var provider = ClientCredentialsTokenProviderTests.BuildProvider(new TokenEndpointStub(3600));

		var first = provider.GetRequiredService<IClientCredentialsTokenProvider>();
		var second = provider.GetRequiredService<IClientCredentialsTokenProvider>();

		Assert.Same(first, second);
		Assert.IsType<ClientCredentialsTokenProvider>(first);
	}

	[Fact]
	public async Task ShouldGiveAServiceClientItsBaseAddressTimeoutAndToken()
	{
		var edge = new EdgeStub();
		using var provider = ClientCredentialsTokenProviderTests.BuildProvider(
			new TokenEndpointStub(3600),
			services => services
				.AddPiggyMetricsServiceClient<EdgeTestClient>("statistics", EdgeAddress)
				.ConfigurePrimaryHttpMessageHandler(() => edge));

		var client = provider.GetRequiredService<EdgeTestClient>();
		using var response = await client.Http.GetAsync("/statistics/demo");

		Assert.Equal(EdgeAddress, client.Http.BaseAddress);
		Assert.Equal(TimeSpan.FromMilliseconds(10000), client.Http.Timeout);
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal("http://statistics-service:7000/statistics/demo", Assert.Single(edge.Uris)!.ToString());
		Assert.Equal("Bearer token-1", Assert.Single(edge.Authorizations));
	}

	[Fact]
	public async Task ShouldGiveATokenFreeEdgeClientItsBaseAddressAndTimeoutAndNoToken()
	{
		var edge = new EdgeStub();
		using var provider = ClientCredentialsTokenProviderTests.BuildProvider(
			new TokenEndpointStub(3600),
			services => services
				.AddPiggyMetricsEdgeClient<EdgeTestClient>("rates", new Uri("http://api.exchangeratesapi.io"))
				.ConfigurePrimaryHttpMessageHandler(() => edge));

		var client = provider.GetRequiredService<EdgeTestClient>();
		using var response = await client.Http.GetAsync("/latest");

		Assert.Equal(new Uri("http://api.exchangeratesapi.io"), client.Http.BaseAddress);
		Assert.Equal(TimeSpan.FromMilliseconds(10000), client.Http.Timeout);
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Null(Assert.Single(edge.Authorizations));
	}

	[Fact]
	public void ShouldNotUseTheGatewaysTimeoutForAServiceEdge()
	{
		Assert.NotEqual(20000, ServiceClientOptions.SourceTimeoutMilliseconds);
		Assert.Equal(10000, ServiceClientOptions.SourceTimeoutMilliseconds);
	}
}
