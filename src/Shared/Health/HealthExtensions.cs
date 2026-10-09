using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PiggyMetrics.Shared.Health;

// Reproduces the source's actuator health contract: GET /actuator/health answers {"status":"UP"} with 200, and
// {"status":"DOWN"} with 503, with no detail body.
public static class HealthExtensions
{
	public const string HealthPath = "/actuator/health";
	public const string UpBody = "{\"status\":\"UP\"}";
	public const string DownBody = "{\"status\":\"DOWN\"}";

	public static IServiceCollection AddPiggyMetricsHealthChecks(this IServiceCollection services)
	{
		services.AddHealthChecks();
		return services;
	}

	public static IEndpointRouteBuilder MapPiggyMetricsHealthChecks(this IEndpointRouteBuilder endpoints)
	{
		var options = new HealthCheckOptions
		{
			ResponseWriter = WriteStatusAsync,
		};
		options.ResultStatusCodes[HealthStatus.Healthy] = StatusCodes.Status200OK;
		options.ResultStatusCodes[HealthStatus.Degraded] = StatusCodes.Status200OK;
		options.ResultStatusCodes[HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable;

		endpoints.MapHealthChecks(HealthPath, options).AllowAnonymous();
		return endpoints;
	}

	private static Task WriteStatusAsync(HttpContext context, HealthReport report)
	{
		context.Response.ContentType = "application/json";
		var body = report.Status == HealthStatus.Unhealthy ? DownBody : UpBody;
		return context.Response.WriteAsync(body);
	}
}
