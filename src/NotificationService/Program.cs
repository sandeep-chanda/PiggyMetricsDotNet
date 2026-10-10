using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Formatters;
using MongoDB.Driver;
using PiggyMetrics.NotificationService.Client;
using PiggyMetrics.NotificationService.Config;
using PiggyMetrics.NotificationService.Hosting;
using PiggyMetrics.NotificationService.Repository;
using PiggyMetrics.NotificationService.Service;
using PiggyMetrics.Shared.Hosting;
using PiggyMetrics.Shared.Http;
using PiggyMetrics.Shared.Security;
using NotificationServiceApi = PiggyMetrics.NotificationService.Service.NotificationService;
using RecipientServiceApi = PiggyMetrics.NotificationService.Service.RecipientService;

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
    var clientSecret = NotificationEnvironment.ResolvePlaceholder(builder.Configuration["security:oauth2:client:clientSecret"]);
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

var mongo = NotificationMongoSettings.From(builder.Configuration);
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongo.ConnectionString));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(mongo.Database));
builder.Services.AddSingleton<RecipientRepository, MongoRecipientRepository>();
builder.Services.AddSingleton<RecipientServiceApi, RecipientServiceImpl>();
builder.Services.AddSingleton<IMailSender, SmtpMailSender>();
builder.Services.AddSingleton<EmailService, EmailServiceImpl>();
builder.Services.AddSingleton<AccountServiceClient, AccountServiceClientImpl>();
builder.Services.AddSingleton<NotificationServiceApi, NotificationServiceImpl>();
builder.Services.AddHostedService<NotificationScheduleHostedService>();

builder.Services.AddHttpClient("account-service", (serviceProvider, client) =>
{
    var baseUrl = serviceProvider.GetRequiredService<IConfiguration>()["clients:account-service"]
        ?? "http://account-service:6000/";
    if (!baseUrl.EndsWith('/'))
    {
        baseUrl += "/";
    }

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = EdgeHttpDefaults.Timeout;
});

if (string.Equals(Assembly.GetEntryAssembly()?.GetName().Name, "PiggyMetrics.NotificationService", StringComparison.Ordinal))
{
    var port = builder.Configuration.GetValue("server:port", 8000);
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var app = builder.Build();
var contextPath = builder.Configuration["server:servlet:context-path"] ?? "/notifications";
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
            .CreateLogger("PiggyMetrics.NotificationService.Controller.ErrorHandler");
        logger.LogInformation(ex, "Returning HTTP 400 Bad Request");
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapPiggyMetricsHealthChecks();
app.MapControllers();
app.Run();

public partial class Program { }
