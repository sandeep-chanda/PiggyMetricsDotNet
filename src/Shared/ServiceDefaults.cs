using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using PiggyMetrics.Shared.Health;
using PiggyMetrics.Shared.Json;

namespace PiggyMetrics.Shared;

public sealed record ServiceIdentity(string Name);

// The composition every PiggyMetrics .NET service starts from: the shared JSON contract, the actuator health
// endpoint, and the authentication and authorization middleware slots.
public static class ServiceDefaults
{
	public const string ContextPathConfigurationKey = "Server:Servlet:ContextPath";

	public static WebApplicationBuilder AddPiggyMetricsDefaults(
		this WebApplicationBuilder builder,
		string applicationName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

		builder.Services.AddSingleton(new ServiceIdentity(applicationName));
		builder.Services
			.AddControllers()
			.AddJsonOptions(options => PiggyMetricsJson.Configure(options.JsonSerializerOptions));
		builder.Services.ConfigureHttpJsonOptions(options => PiggyMetricsJson.Configure(options.SerializerOptions));
		builder.Services.AddAuthentication();
		builder.Services.AddAuthorization();
		builder.Services.AddPiggyMetricsHealthChecks();

		return builder;
	}

	public static WebApplication UsePiggyMetricsDefaults(this WebApplication app)
	{
		var contextPath = app.Configuration[ContextPathConfigurationKey];
		if (!string.IsNullOrWhiteSpace(contextPath))
		{
			app.UsePathBase(contextPath);
		}

		app.UseRouting();
		app.UseAuthentication();
		app.UseAuthorization();
		app.MapPiggyMetricsHealthChecks();
		app.MapControllers();

		return app;
	}
}
