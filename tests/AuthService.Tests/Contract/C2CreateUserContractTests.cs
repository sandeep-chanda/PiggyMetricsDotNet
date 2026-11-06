using System.Net;
using Xunit;

namespace PiggyMetrics.AuthService.Tests.Contract;

public class C2CreateUserContractTests
{
    [Fact]
    public async Task Post_with_server_token_returns_200_and_with_user_token_returns_403()
    {
        await using var destination = await AuthDestination.StartAsync();

        var asServer = await Send.PostJsonAsync(
            destination.Client,
            "/uaa/users",
            UserJson("created-by-server"),
            destination.Tokens.Server.AccessToken);
        Assert.Equal(HttpStatusCode.OK, asServer.StatusCode);

        var asUser = await Send.PostJsonAsync(
            destination.Client,
            "/uaa/users",
            UserJson("created-by-user"),
            destination.Tokens.User.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, asUser.StatusCode);
    }

    private static string UserJson(string username)
    {
        return "{\"username\":\"" + username + "\",\"password\":\"secret\"}";
    }
}
