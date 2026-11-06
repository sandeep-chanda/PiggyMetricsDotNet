using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Driver;
using PiggyMetrics.Shared.Hosting;
using PiggyMetrics.Shared.Http;
using PiggyMetrics.Shared.Security;
using PiggyMetrics.StatisticsService.Client;
using PiggyMetrics.StatisticsService.Config;
using PiggyMetrics.StatisticsService.Repository;
using PiggyMetrics.StatisticsService.Service;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPiggyMetricsSharedDefaults();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("user", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("server", policy => policy.RequireAuthenticatedUser().RequireClaim("scope", "server"));
});
builder.Services.PostConfigure<UserInfoBearerOptions>(UserInfoBearerOptions.DefaultScheme, options =>
{
    var clientId = builder.Configuration["security:oauth2:client:clientId"];
    if (!string.IsNullOrEmpty(clientId))
    {
        options.ClientId = clientId;
    }
});
builder.Services.Configure<ClientCredentialsOptions>(options =>
{
    var accessTokenUri = builder.Configuration["security:oauth2:client:accessTokenUri"];
    var clientId = builder.Configuration["security:oauth2:client:clientId"];
    var clientSecret = StatisticsSettings.ResolvePlaceholder(builder.Configuration["security:oauth2:client:clientSecret"]);
    var grantType = builder.Configuration["security:oauth2:client:grant-type"];
    var scope = builder.Configuration["security:oauth2:client:scope"];
    if (!string.IsNullOrEmpty(accessTokenUri))
    {
        options.AccessTokenUri = accessTokenUri;
    }

    if (!string.IsNullOrEmpty(clientId))
    {
        options.ClientId = clientId;
    }

    if (!string.IsNullOrEmpty(clientSecret))
    {
        options.ClientSecret = clientSecret;
    }

    if (!string.IsNullOrEmpty(grantType))
    {
        options.GrantType = grantType;
    }

    if (!string.IsNullOrEmpty(scope))
    {
        options.Scope = scope;
    }
});
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
    options.JsonSerializerOptions.Converters.Clear();
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.JsonSerializerOptions.Converters.Add(new IsoDateTimeOffsetConverter());
});

var mongo = StatisticsMongoSettings.From(builder.Configuration);
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongo.ConnectionString));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(mongo.Database));
builder.Services.AddSingleton<DataPointRepository, MongoDataPointRepository>();
builder.Services.AddSingleton<ExchangeRatesClientFallback>();
builder.Services.AddSingleton<ExchangeRatesClient, ExchangeRatesClientImpl>();
builder.Services.AddSingleton<ExchangeRatesService, ExchangeRatesServiceImpl>();
builder.Services.AddSingleton<StatisticsService, StatisticsServiceImpl>();

builder.Services.AddHttpClient("rates-client", (serviceProvider, client) =>
{
    var ratesUrl = serviceProvider.GetRequiredService<IConfiguration>()["rates:url"]
        ?? "https://api.exchangeratesapi.io";
    if (!ratesUrl.EndsWith('/'))
    {
        ratesUrl += "/";
    }

    client.BaseAddress = new Uri(ratesUrl);
    client.Timeout = EdgeHttpDefaults.Timeout;
});

if (string.Equals(Assembly.GetEntryAssembly()?.GetName().Name, "PiggyMetrics.StatisticsService", StringComparison.Ordinal))
{
    var port = builder.Configuration.GetValue("server:port", 7000);
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var app = builder.Build();
var contextPath = builder.Configuration["server:servlet:context-path"] ?? "/statistics";
if (!contextPath.StartsWith('/'))
{
    contextPath = "/" + contextPath;
}

app.UsePathBase(contextPath);
app.UseAuthentication();
app.UseAuthorization();
app.MapPiggyMetricsHealthChecks();
app.MapControllers();
app.Run();

public partial class Program { }
