using System.Net;
using System.Text.Json;
using Xunit;

namespace PiggyMetrics.AuthService.Tests.Contract;

public class C1CurrentUserContractTests
{
    [Fact]
    public async Task Get_with_user_token_returns_source_body_and_without_one_returns_401()
    {
        await using var destination = await AuthDestination.StartAsync();

        var missing = await destination.Client.GetAsync("/uaa/users/current");
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        var response = await Send.GetAsync(destination.Client, "/uaa/users/current", destination.Tokens.User.AccessToken);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.False(root.TryGetProperty("error", out _));
        Assert.Equal("demo", root.GetProperty("name").GetString());
        Assert.Equal("demo", root.GetProperty("username").GetString());
        Assert.True(root.GetProperty("authenticated").GetBoolean());
        Assert.False(root.GetProperty("clientOnly").GetBoolean());
        var oauth = root.GetProperty("oauth2Request");
        Assert.Equal("browser", oauth.GetProperty("clientId").GetString());
        var scopes = oauth.GetProperty("scope").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains("ui", scopes);
    }
}
