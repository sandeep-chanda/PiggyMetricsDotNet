using System.Reflection;
using PiggyMetrics.Gateway.Cutover;
using PiggyMetrics.Gateway.Proxy;
using PiggyMetrics.Shared.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPiggyMetricsSharedDefaults();
builder.Services.AddSingleton<ZuulRouteTable>();
builder.Services.AddSingleton<SourceGatewayCutover>();
builder.Services.AddHttpClient("zuul")
    .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        return new SocketsHttpHandler
        {
            ConnectTimeout = GatewayTimeouts.Connect(configuration),
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = System.Net.DecompressionMethods.None
        };
    })
    .ConfigureHttpClient((serviceProvider, client) =>
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        client.Timeout = GatewayTimeouts.Command(configuration);
    });

if (string.Equals(Assembly.GetEntryAssembly()?.GetName().Name, GatewayHost.EntryAssemblyName, StringComparison.Ordinal))
{
    builder.WebHost.UseUrls(GatewayHost.ListenUrl(builder.Configuration));
}

var app = builder.Build();
app.UseMiddleware<ZuulProxyMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapPiggyMetricsHealthChecks();
app.Run();

public partial class Program { }
