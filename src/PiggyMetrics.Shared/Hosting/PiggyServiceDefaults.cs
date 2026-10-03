using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PiggyMetrics.Shared.Json;
using PiggyMetrics.Shared.Security;

namespace PiggyMetrics.Shared.Hosting;

/// <summary>Identity of one service: its name and the source's server.servlet.context-path.</summary>
public sealed class PiggyServiceOptions
{
    public required string ServiceName { get; init; }

    public required string PathBase { get; init; }

    public Action<AuthorizationBuilder>? ConfigureAuthorization { get; init; }
}

/// <summary>
/// The cross-cutting host behaviour every service shares: the source's JSON shape, its authorization
/// policies, and an actuator-shaped health endpoint under the service's own path (decisions D-010, D-011).
/// </summary>
public static class PiggyServiceDefaults
{
    public const string HealthPath = "/actuator/health";
    public const string HealthContentType = "application/json;charset=UTF-8";
    public const string StatusUp = "UP";
    public const string StatusDown = "DOWN";

    public static WebApplicationBuilder AddPiggyServiceDefaults(
        this WebApplicationBuilder builder,
        PiggyServiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddHttpContextAccessor();

        // Registers IAuthenticationSchemeProvider for every host, so UsePiggyServiceDefaults can call
        // UseAuthentication even in a host that registers no scheme of its own.
        builder.Services.AddAuthentication();
        builder.Services.ConfigureHttpJsonOptions(json => PiggyJson.Configure(json.SerializerOptions));
        builder.Services.AddControllers().AddJsonOptions(json => PiggyJson.Configure(json.JsonSerializerOptions));
        builder.Services.AddPiggyAuthorization(options.ConfigureAuthorization);
        builder.Services.AddHealthChecks();

        return builder;
    }

    public static WebApplication UsePiggyServiceDefaults(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = app.Services.GetRequiredService<PiggyServiceOptions>();
        if (options.PathBase.Length > 0)
        {
            app.UsePathBase(options.PathBase);
        }

        // Routing is called here, after the path base, so endpoints match the stripped path. Calling it
        // explicitly also stops WebApplication from inserting its own UseRouting ahead of this pipeline.
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHealthChecks(
                HealthPath,
                new HealthCheckOptions
                {
                    ResponseWriter = WriteHealthAsync,
                    ResultStatusCodes =
                    {
                        [HealthStatus.Healthy] = StatusCodes.Status200OK,
                        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
                        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
                    },
                })
            .AllowAnonymous();

        app.MapControllers();

        return app;
    }

    private static Task WriteHealthAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = HealthContentType;

        var body = new Dictionary<string, string>
        {
            ["status"] = report.Status == HealthStatus.Healthy ? StatusUp : StatusDown,
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(body, PiggyJson.Default));
    }
}
