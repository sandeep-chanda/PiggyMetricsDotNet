using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using PiggyMetrics.AccountService.Client;
using PiggyMetrics.AccountService.Domain;
using PiggyMetrics.Shared.Http;
using Xunit;

namespace PiggyMetrics.AccountService.Tests.Client;

public class AuthServiceClientTest
{
    [Fact]
    public void createUser_propagates_when_auth_service_fails()
    {
        var auth = new RecordingHandler { Status = HttpStatusCode.InternalServerError };
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<HttpClientFactoryOptions>("token", options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                    {
                        handlerBuilder.PrimaryHandler = new RecordingHandler
                        {
                            Status = HttpStatusCode.OK,
                            Body = "{\"access_token\":\"issued\",\"expires_in\":3600}"
                        };
                    });
                });
                services.PostConfigure<HttpClientFactoryOptions>("auth-service", options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                    {
                        handlerBuilder.PrimaryHandler = auth;
                    });
                });
            });
        });

        var clients = factory.Services.GetRequiredService<IHttpClientFactory>();
        var http = clients.CreateClient("auth-service");
        Assert.Equal(TimeSpan.FromMilliseconds(10000), http.Timeout);
        Assert.Equal(EdgeHttpDefaults.Timeout, http.Timeout);

        var client = factory.Services.GetRequiredService<AuthServiceClient>();
        Assert.Throws<HttpRequestException>(() => client.CreateUser(new User
        {
            Username = "test",
            Password = "password"
        }));

        Assert.Equal(HttpMethod.Post, auth.Method);
        Assert.Equal("/uaa/users", auth.Path);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; init; }

        public string Body { get; init; } = string.Empty;

        public HttpMethod? Method { get; private set; }

        public string? Path { get; private set; }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Remember(request);
            return new HttpResponseMessage(Status)
            {
                Content = new StringContent(Body, System.Text.Encoding.UTF8, "application/json")
            };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Send(request, cancellationToken));
        }

        private void Remember(HttpRequestMessage request)
        {
            Method = request.Method;
            Path = request.RequestUri?.AbsolutePath;
        }
    }
}
