using System.Net;
using System.Text;
using Xunit;

namespace PiggyMetrics.AuthService.Tests.Contract;

public class AuthMatrixTests
{
    [Fact]
    public async Task C1_user_policy_statuses()
    {
        await using var destination = await AuthDestination.StartAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetCurrent(destination, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetCurrent(destination, destination.Tokens.User.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetCurrent(destination, destination.Tokens.Server.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task C2_server_policy_statuses()
    {
        await using var destination = await AuthDestination.StartAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateUser(destination, "no-token-user", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateUser(destination, "user-token-user", destination.Tokens.User.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CreateUser(destination, "server-token-user", destination.Tokens.Server.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task C3_client_credentials_policy_statuses()
    {
        await using var destination = await AuthDestination.StartAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Token(destination, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Token(destination, destination.Tokens.User.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Token(destination, destination.Tokens.Server.AccessToken)).StatusCode);
    }

    private static Task<HttpResponseMessage> GetCurrent(AuthDestination destination, string? bearer)
    {
        return Send.GetAsync(destination.Client, "/uaa/users/current", bearer);
    }

    private static Task<HttpResponseMessage> CreateUser(AuthDestination destination, string username, string? bearer)
    {
        var json = "{\"username\":\"" + username + "\",\"password\":\"secret\"}";
        return Send.PostJsonAsync(destination.Client, "/uaa/users", json, bearer);
    }

    private static async Task<HttpResponseMessage> Token(AuthDestination destination, string? bearer)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/uaa/oauth/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials"
            })
        };
        if (!string.IsNullOrEmpty(bearer))
        {
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearer);
        }

        return await destination.Client.SendAsync(request);
    }
}
