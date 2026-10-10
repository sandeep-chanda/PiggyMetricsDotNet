using System.Text;
using PiggyMetrics.Gateway.Cutover;

namespace PiggyMetrics.Gateway.Proxy;

public sealed class ZuulProxyMiddleware
{
    private static readonly HashSet<string> HopByHop = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailer",
        "Transfer-Encoding",
        "Upgrade",
        "Host"
    };

    private readonly RequestDelegate _next;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ZuulRouteTable _routes;
    private readonly SourceGatewayCutover _cutover;
    private readonly ILogger<ZuulProxyMiddleware> _logger;

    public ZuulProxyMiddleware(
        RequestDelegate next,
        IHttpClientFactory httpClientFactory,
        ZuulRouteTable routes,
        SourceGatewayCutover cutover,
        ILogger<ZuulProxyMiddleware> logger)
    {
        _next = next;
        _httpClientFactory = httpClientFactory;
        _routes = routes;
        _cutover = cutover;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_routes.TryMatch(context.Request.Path, out var route, out var baseUrl))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "/";
        var forwardPath = ZuulRoutes.ForwardPath(path, route);
        var query = context.Request.QueryString.Value;
        var liveBase = baseUrl;
        CutoverDecision? decision = null;
        if (_cutover.TryDecide(route.Path, out var found))
        {
            decision = found;
            liveBase = found.ActiveUnit;
        }

        byte[]? body = null;
        if (HasBody(context.Request))
        {
            using var buffer = new MemoryStream();
            await context.Request.Body.CopyToAsync(buffer, context.RequestAborted);
            body = buffer.ToArray();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("zuul");
            using var liveRequest = CreateRequest(context, route, Target(liveBase, forwardPath, query), body);
            using var liveResponse = await client.SendAsync(
                liveRequest,
                HttpCompletionOption.ResponseHeadersRead,
                context.RequestAborted);
            var liveBody = await CopyResponseAsync(context, liveResponse);

            if (decision is { Mode: CutoverMode.Shadow })
            {
                await ShadowAsync(context, client, route, decision.Value, forwardPath, query, body, liveBody);
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidOperationException)
        {
            _logger.LogInformation(ex, "Gateway route {Route} failed", route.Name);
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            }
        }
    }

    private async Task ShadowAsync(
        HttpContext context,
        HttpClient client,
        ZuulRouteDefinition route,
        CutoverDecision decision,
        string forwardPath,
        string? query,
        byte[]? body,
        string liveBody)
    {
        try
        {
            using var shadowRequest = CreateRequest(
                context,
                route,
                Target(decision.DestinationUnit, forwardPath, query),
                body);
            using var shadowResponse = await client.SendAsync(
                shadowRequest,
                HttpCompletionOption.ResponseHeadersRead,
                context.RequestAborted);
            var shadowBytes = await shadowResponse.Content.ReadAsByteArrayAsync(context.RequestAborted);
            _cutover.RecordShadowResult(
                route.Path,
                context.Response.StatusCode,
                liveBody,
                (int)shadowResponse.StatusCode,
                Encoding.UTF8.GetString(shadowBytes));
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidOperationException)
        {
            _logger.LogInformation(ex, "Gateway shadow {Route} failed", route.Name);
            _cutover.RecordShadowResult(route.Path, context.Response.StatusCode, liveBody, 0, string.Empty);
        }
    }

    private static Uri Target(string baseUrl, string forwardPath, string? query)
    {
        return new Uri(baseUrl.TrimEnd('/') + forwardPath + query);
    }

    private static HttpRequestMessage CreateRequest(
        HttpContext context,
        ZuulRouteDefinition route,
        Uri target,
        byte[]? body)
    {
        var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), target);
        if (body is { Length: > 0 })
        {
            request.Content = new ByteArrayContent(body);
            if (!string.IsNullOrEmpty(context.Request.ContentType))
            {
                request.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
            }
        }

        foreach (var header in context.Request.Headers)
        {
            if (HopByHop.Contains(header.Key) || route.SensitiveHeaders.Contains(header.Key))
            {
                continue;
            }

            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
                || header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            {
                request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        return request;
    }

    private static async Task<string> CopyResponseAsync(HttpContext context, HttpResponseMessage response)
    {
        context.Response.StatusCode = (int)response.StatusCode;
        CopyHeaders(response.Headers, context.Response.Headers);
        CopyHeaders(response.Content.Headers, context.Response.Headers);
        context.Response.Headers.Remove("transfer-encoding");
        var bytes = await response.Content.ReadAsByteArrayAsync(context.RequestAborted);
        await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
        return Encoding.UTF8.GetString(bytes);
    }

    private static void CopyHeaders(IEnumerable<KeyValuePair<string, IEnumerable<string>>> source, IHeaderDictionary target)
    {
        foreach (var header in source)
        {
            if (HopByHop.Contains(header.Key))
            {
                continue;
            }

            target[header.Key] = header.Value.ToArray();
        }
    }

    private static bool HasBody(HttpRequest request)
    {
        if (HttpMethods.IsGet(request.Method)
            || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsTrace(request.Method))
        {
            return false;
        }

        return request.ContentLength is > 0 || request.Headers.ContainsKey("Transfer-Encoding");
    }
}
