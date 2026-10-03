using System.Text.Json;
using Microsoft.AspNetCore.Http;
using PiggyMetrics.Shared.Json;

namespace PiggyMetrics.Shared.Security;

/// <summary>
/// The three failure responses spring-security-oauth2 emits for the source's configuration
/// (decision D-005): status, body, Cache-Control, Pragma and WWW-Authenticate.
/// </summary>
public static class PiggyOAuthErrors
{
    public const string UnauthorizedCode = "unauthorized";
    public const string InvalidTokenCode = "invalid_token";
    public const string AccessDeniedCode = "access_denied";
    public const string FullAuthenticationRequired = "Full authentication is required to access this resource";
    public const string AccessIsDenied = "Access is denied";
    public const string ContentType = "application/json;charset=UTF-8";

    public static Task WriteUnauthorizedAsync(HttpResponse response) =>
        WriteAsync(response, StatusCodes.Status401Unauthorized, UnauthorizedCode, FullAuthenticationRequired, true);

    public static Task WriteInvalidTokenAsync(HttpResponse response, string presentedToken) =>
        WriteAsync(response, StatusCodes.Status401Unauthorized, InvalidTokenCode, presentedToken, true);

    public static Task WriteAccessDeniedAsync(HttpResponse response) =>
        WriteAsync(response, StatusCodes.Status403Forbidden, AccessDeniedCode, AccessIsDenied, false);

    private static Task WriteAsync(
        HttpResponse response,
        int statusCode,
        string error,
        string description,
        bool writeChallengeHeader)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.StatusCode = statusCode;
        response.ContentType = ContentType;
        response.Headers.CacheControl = "no-store";
        response.Headers.Pragma = "no-cache";

        if (writeChallengeHeader)
        {
            response.Headers.WWWAuthenticate =
                $"Bearer realm=\"{PiggyBearerDefaults.Realm}\", error=\"{error}\", error_description=\"{description}\"";
        }

        var body = new Dictionary<string, string>
        {
            ["error"] = error,
            ["error_description"] = description,
        };

        return response.WriteAsync(JsonSerializer.Serialize(body, PiggyJson.Default));
    }
}
