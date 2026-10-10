using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Contrib.HttpClient;
using PiggyMetrics.Shared.Security;
using Xunit;

namespace PiggyMetrics.Shared.Tests;

public class UserInfoBearerPolicyTests
{
    [Fact]
    public void Scheme_and_userinfo_url_match_resource_servers()
    {
        Assert.Equal("Bearer", UserInfoBearerOptions.DefaultScheme);
        Assert.Equal(
            "http://auth-service:5000/uaa/users/current",
            new UserInfoBearerOptions().UserInfoEndpointUrl);
    }

    [Fact]
    public async Task Refuses_token_when_userinfo_http_status_is_not_success()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.SetupRequest(HttpMethod.Get, "http://auth-service:5000/uaa/users/current")
            .ReturnsResponse(HttpStatusCode.Unauthorized, new StringContent("no", Encoding.UTF8, "text/plain"));

        using var server = CreateServer(handler.Object);
        var client = server.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer not-issued");
        var response = await client.GetAsync("/secure");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refuses_request_when_authorization_header_is_missing()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.SetupRequest(HttpMethod.Get, "http://auth-service:5000/uaa/users/current")
            .ReturnsResponse(HttpStatusCode.OK, new StringContent("{}", Encoding.UTF8, "application/json"));

        using var server = CreateServer(handler.Object);
        var client = server.CreateClient();
        var response = await client.GetAsync("/secure");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static TestServer CreateServer(HttpMessageHandler userInfoHandler)
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IHttpClientFactory>(_ =>
                {
                    var mock = new Mock<IHttpClientFactory>();
                    mock.Setup(f => f.CreateClient("userinfo"))
                        .Returns(new HttpClient(userInfoHandler));
                    return mock.Object;
                });
                services.AddAuthentication(UserInfoBearerOptions.DefaultScheme)
                    .AddUserInfoBearer();
                services.AddAuthorization();
                services.AddRouting();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/secure", async ctx =>
                    {
                        if (ctx.User.Identity?.IsAuthenticated != true)
                        {
                            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return;
                        }

                        await ctx.Response.WriteAsync(ctx.User.Identity!.Name ?? "");
                    }).RequireAuthorization();
                });
            });
        return new TestServer(builder);
    }
}
