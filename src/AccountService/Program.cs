using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Formatters;
using MongoDB.Driver;
using PiggyMetrics.AccountService.Client;
using PiggyMetrics.AccountService.Config;
using PiggyMetrics.AccountService.Repository;
using PiggyMetrics.AccountService.Service;
using PiggyMetrics.Shared.Hosting;
using PiggyMetrics.Shared.Http;
using PiggyMetrics.Shared.Security;
using AccountServiceApi = PiggyMetrics.AccountService.Service.AccountService;

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
    var clientSecret = AccountSettings.ResolvePlaceholder(builder.Configuration["security:oauth2:client:clientSecret"]);
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
builder.Services.AddControllers(options =>
{
    options.OutputFormatters.RemoveType<HttpNoContentOutputFormatter>();
}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
    options.JsonSerializerOptions.Converters.Clear();
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.JsonSerializerOptions.Converters.Add(new IsoDateTimeOffsetConverter());
});

var mongo = AccountMongoSettings.From(builder.Configuration);
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongo.ConnectionString));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(mongo.Database));
builder.Services.AddSingleton<AccountRepository, MongoAccountRepository>();
builder.Services.AddSingleton<StatisticsServiceClientFallback>();
builder.Services.AddSingleton<StatisticsServiceClient, StatisticsServiceClientImpl>();
builder.Services.AddSingleton<AuthServiceClient, AuthServiceClientImpl>();
builder.Services.AddSingleton<AccountServiceApi, AccountServiceImpl>();

builder.Services.AddHttpClient("auth-service", (serviceProvider, client) =>
{
    var baseUrl = serviceProvider.GetRequiredService<IConfiguration>()["clients:auth-service"]
        ?? "http://auth-service:5000/";
    if (!baseUrl.EndsWith('/'))
    {
        baseUrl += "/";
    }

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = EdgeHttpDefaults.Timeout;
});
builder.Services.AddHttpClient("statistics-service", (serviceProvider, client) =>
{
    var baseUrl = serviceProvider.GetRequiredService<IConfiguration>()["clients:statistics-service"]
        ?? "http://statistics-service:7000/";
    if (!baseUrl.EndsWith('/'))
    {
        baseUrl += "/";
    }

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = EdgeHttpDefaults.Timeout;
});

if (string.Equals(Assembly.GetEntryAssembly()?.GetName().Name, "PiggyMetrics.AccountService", StringComparison.Ordinal))
{
    var port = builder.Configuration.GetValue("server:port", 6000);
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var app = builder.Build();
var contextPath = builder.Configuration["server:servlet:context-path"] ?? "/accounts";
if (!contextPath.StartsWith('/'))
{
    contextPath = "/" + contextPath;
}

app.UsePathBase(contextPath);
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (ArgumentException ex) when (ex is not ArgumentNullException)
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("PiggyMetrics.AccountService.Controller.ErrorHandler");
        logger.LogInformation(ex, "Returning HTTP 400 Bad Request");
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
});
// Match routes after the context path is removed. Otherwise POST /accounts is
// treated as GET /{name} and rejected with 405.
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapPiggyMetricsHealthChecks();
app.MapControllers();
app.Run();

public partial class Program { }
