using System.Net;
using System.Text;

namespace PiggyMetrics.Parity.Tests;

/// <summary>
/// One stub definition for every external client. Source and destination both
/// delegate here so a given request yields the same status and body.
/// </summary>
public static class ExternalStubCatalog
{
    public const string UserToken = "user-token";
    public const string ServerToken = "server-token";
    public const string IssuedAccessToken = "issued";

    public static readonly string[] Services = ["account-service", "statistics-service", "notification-service"];

    public static HttpResponseMessage UserInfo(string serviceName, HttpRequestMessage request)
    {
        var token = request.Headers.Authorization?.Parameter ?? string.Empty;
        var (status, json) = UserInfoBody(serviceName, token);
        return Json(status, json);
    }

    public static (HttpStatusCode Status, string Body) UserInfoBody(string serviceName, string token)
    {
        return token switch
        {
            UserToken => (HttpStatusCode.OK,
                "{\"username\":\"demo\",\"oauth2Request\":{\"clientId\":\"browser\",\"scope\":[\"ui\"]}}"),
            ServerToken => (HttpStatusCode.OK,
                "{\"name\":\"" + serviceName + "\",\"oauth2Request\":{\"clientId\":\"" + serviceName + "\",\"scope\":[\"server\"]}}"),
            _ => (HttpStatusCode.Unauthorized, "{\"error\":\"invalid_token\"}")
        };
    }

    public static HttpResponseMessage Token(HttpRequestMessage request)
    {
        return Json(HttpStatusCode.OK, "{\"access_token\":\"" + IssuedAccessToken + "\",\"expires_in\":3600}");
    }

    public static HttpResponseMessage Rates(HttpRequestMessage request)
    {
        return Json(HttpStatusCode.OK,
            "{\"base\":\"USD\",\"rates\":{\"USD\":1,\"EUR\":0.8,\"RUB\":80}}");
    }

    public static HttpResponseMessage EmptyOk(HttpRequestMessage request)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        };
    }

    public static CatalogHandler Handler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        return new CatalogHandler(respond);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}

public sealed class CatalogHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public CatalogHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    public List<string> Calls { get; } = new();

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls.Add(request.Method + " " + request.RequestUri);
        return _respond(request);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Send(request, cancellationToken));
    }
}
