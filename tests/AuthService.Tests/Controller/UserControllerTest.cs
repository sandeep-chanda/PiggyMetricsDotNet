using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PiggyMetrics.AuthService.Controller;
using PiggyMetrics.AuthService.Domain;
using PiggyMetrics.AuthService.Service;
using Xunit;

namespace PiggyMetrics.AuthService.Tests.Controller;

public class UserControllerTest
{
    private readonly Mock<UserService> _userService = new();

    [Fact]
    public async Task shouldCreateNewUser()
    {
        var user = new User
        {
            Username = "test",
            Password = "password"
        };
        var json = JsonSerializer.Serialize(user);

        using var server = CreateServer();
        var client = server.CreateClient();
        var response = await client.PostAsync(
            "/users",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task shouldFailWhenUserIsNotValid()
    {
        var user = new User
        {
            Username = "t",
            Password = "p"
        };
        var json = JsonSerializer.Serialize(user);

        using var server = CreateServer();
        var client = server.CreateClient();
        var response = await client.PostAsync(
            "/users",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void shouldReturnCurrentUser()
    {
        var controller = new UserController(_userService.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Name, "test")],
                    authenticationType: "Bearer"))
            }
        };

        var result = controller.GetUser();
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode ?? StatusCodes.Status200OK);
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("test", doc.RootElement.GetProperty("name").GetString());
    }

    private TestServer CreateServer()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<UserService>(_userService.Object);
                services.AddControllers().AddApplicationPart(typeof(UserController).Assembly);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });
        return new TestServer(builder);
    }
}
