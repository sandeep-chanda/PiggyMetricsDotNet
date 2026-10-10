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
    private readonly ILogger<ZuulProxyMiddleware> _logger;

    public ZuulProxyMiddleware(
        RequestDelegate next,
        IHttpClientFactory httpClientFactory,
        ZuulRouteTable routes,
        ILogger<ZuulProxyMiddleware> logger)
    {
        _next = next;
        _httpClientFactory = httpClientFactory;
        _routes = routes;
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
        var target = new Uri(baseUrl.TrimEnd('/') + forwardPath + context.Request.QueryString.Value);

        try
        {
            using var request = await CreateRequestAsync(context, route, target);
            var client = _httpClientFactory.CreateClient("zuul");
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                context.RequestAborted);
            await CopyResponseAsync(context, response);
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

    private static async Task<HttpRequestMessage> CreateRequestAsync(
        HttpContext context,
        ZuulRouteDefinition route,
        Uri target)
    {
        var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), target);
        if (HasBody(context.Request))
        {
            var buffer = new MemoryStream();
            await context.Request.Body.CopyToAsync(buffer, context.RequestAborted);
            buffer.Position = 0;
            request.Content = new StreamContent(buffer);
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

    private static async Task CopyResponseAsync(HttpContext context, HttpResponseMessage response)
    {
        context.Response.StatusCode = (int)response.StatusCode;
        CopyHeaders(response.Headers, context.Response.Headers);
        CopyHeaders(response.Content.Headers, context.Response.Headers);
        context.Response.Headers.Remove("transfer-encoding");
        await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
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
