using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Moq.Contrib.HttpClient;
using PiggyMetrics.Shared.Security;
using Xunit;

namespace PiggyMetrics.Shared.Tests;

public class UserInfoBearerAuthenticationHandlerTests
{
    [Fact]
    public async Task Accepts_token_when_userinfo_returns_principal()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.SetupRequest(HttpMethod.Get, "http://auth-service:5000/uaa/users/current")
            .ReturnsResponse(HttpStatusCode.OK, new StringContent(
                JsonSerializer.Serialize(new
                {
                    username = "demo",
                    oauth2Request = new { clientId = "browser", scope = new[] { "ui" } }
                }), Encoding.UTF8, "application/json"));

        using var server = CreateServer(handler.Object);
        var client = server.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer issued-by-auth");
        var response = await client.GetAsync("/secure");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("demo", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Refuses_token_when_userinfo_returns_error()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.SetupRequest(HttpMethod.Get, "http://auth-service:5000/uaa/users/current")
            .ReturnsResponse(HttpStatusCode.OK, new StringContent(
                "{\"error\":\"Could not fetch user details\"}", Encoding.UTF8, "application/json"));

        using var server = CreateServer(handler.Object);
        var client = server.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer not-issued");
        var response = await client.GetAsync("/secure");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static TestServer CreateServer(HttpMessageHandler userInfoHandler)
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IHttpClientFactory>(sp =>
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
