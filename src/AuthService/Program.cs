using System.Reflection;
using Microsoft.AspNetCore.Authentication;
using MongoDB.Driver;
using PiggyMetrics.AuthService.Config;
using PiggyMetrics.AuthService.Repository;
using PiggyMetrics.AuthService.Security;
using PiggyMetrics.AuthService.Service;
using PiggyMetrics.AuthService.Service.Security;
using PiggyMetrics.Shared.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPiggyMetricsSharedDefaults();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = LocalAccessTokenDefaults.Scheme;
    options.DefaultChallengeScheme = LocalAccessTokenDefaults.Scheme;
    options.DefaultForbidScheme = LocalAccessTokenDefaults.Scheme;
}).AddScheme<AuthenticationSchemeOptions, LocalAccessTokenAuthenticationHandler>(
    LocalAccessTokenDefaults.Scheme,
    _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("user", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("server", policy => policy.RequireAuthenticatedUser().RequireClaim("scope", "server"));
});
builder.Services.AddControllers(options =>
{
    options.Conventions.Add(new AuthEndpointAuthorizationConvention());
});

var mongo = AuthMongoSettings.From(builder.Configuration);
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongo.ConnectionString));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(mongo.Database));
builder.Services.AddSingleton<UserRepository, MongoUserRepository>();
builder.Services.AddSingleton<UserService, UserServiceImpl>();
builder.Services.AddSingleton<MongoUserDetailsService>();
builder.Services.AddSingleton<InMemoryTokenStore>();
builder.Services.AddSingleton(sp => new OAuth2AuthorizationConfig(sp.GetRequiredService<IConfiguration>()));

if (string.Equals(Assembly.GetEntryAssembly()?.GetName().Name, "PiggyMetrics.AuthService", StringComparison.Ordinal))
{
    var port = builder.Configuration.GetValue("server:port", 5000);
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var app = builder.Build();
var contextPath = builder.Configuration["server:servlet:context-path"] ?? "/uaa";
if (!contextPath.StartsWith('/'))
{
    contextPath = "/" + contextPath;
}

app.UsePathBase(contextPath);
app.UseAuthentication();
app.UseAuthorization();
app.MapPiggyMetricsHealthChecks();
app.MapControllers();
app.MapOAuthTokenEndpoint();
app.Run();

public partial class Program { }
