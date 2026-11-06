using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using PiggyMetrics.AuthService.Domain;
using PiggyMetrics.AuthService.Service;

namespace PiggyMetrics.AuthService.Controller;

[ApiController]
[Route("users")]
public class UserController : ControllerBase
{
    private readonly UserService _userService;

    public UserController(UserService userService)
    {
        _userService = userService;
    }

    [HttpGet("current")]
    public IActionResult GetUser()
    {
        var name = User.Identity?.Name ?? string.Empty;
        var clientId = User.FindFirst("client_id")?.Value ?? string.Empty;
        var scopes = User.FindAll("scope").Select(claim => claim.Value).ToArray();
        var username = User.FindFirst("username")?.Value;
        return Ok(new CurrentUserResponse
        {
            Name = name,
            Username = username,
            Authenticated = User.Identity?.IsAuthenticated ?? false,
            ClientOnly = string.IsNullOrEmpty(username),
            OAuth2Request = new OAuth2RequestResponse
            {
                ClientId = clientId,
                Scope = scopes
            }
        });
    }

    [HttpPost]
    public IActionResult CreateUser([FromBody] User? user)
    {
        if (user is null || !ModelState.IsValid)
        {
            return BadRequest();
        }

        _userService.Create(user);
        return Ok();
    }
}

public sealed class CurrentUserResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Username { get; set; }

    [JsonPropertyName("authenticated")]
    public bool Authenticated { get; set; }

    [JsonPropertyName("clientOnly")]
    public bool ClientOnly { get; set; }

    [JsonPropertyName("oauth2Request")]
    public OAuth2RequestResponse OAuth2Request { get; set; } = new();
}

public sealed class OAuth2RequestResponse
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("scope")]
    public string[] Scope { get; set; } = [];
}
