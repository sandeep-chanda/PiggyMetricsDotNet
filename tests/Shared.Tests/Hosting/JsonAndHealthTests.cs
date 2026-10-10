using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PiggyMetrics.Shared.Hosting;
using Xunit;

namespace PiggyMetrics.Shared.Tests;

public class JsonAndHealthTests
{
    [Fact]
    public void Date_format_matches_source_iso_pattern()
    {
        Assert.Equal("yyyy-MM-dd'T'HH:mm:ss.fffK", PiggyMetricsJsonOptions.DateFormat);
    }

    [Fact]
    public void Json_writes_enum_names_iso_dates_and_skips_unknown_fields()
    {
        var options = PiggyMetricsJsonOptions.Create();
        Assert.Equal(JsonUnmappedMemberHandling.Skip, options.UnmappedMemberHandling);

        Assert.Equal("\"USD\"", JsonSerializer.Serialize(ProbeCurrency.USD, options));
        Assert.Equal(ProbeCurrency.EUR, JsonSerializer.Deserialize<ProbeCurrency>("\"EUR\"", options));

        var stamp = new DateTimeOffset(2018, 5, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var json = JsonSerializer.Serialize(stamp, options);
        using var written = JsonDocument.Parse(json);
        Assert.Equal("2018-05-06T07:08:09.010+00:00", written.RootElement.GetString());
        Assert.Equal(stamp, JsonSerializer.Deserialize<DateTimeOffset>(json, options));

        var body = JsonSerializer.Deserialize<ProbeNamed>("{\"name\":\"demo\",\"unknown\":true}", options);
        Assert.NotNull(body);
        Assert.Equal("demo", body!.Name);
    }

    [Fact]
    public async Task Shared_defaults_map_health_and_http_json()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(JsonAndHealthTests).Assembly.GetName().Name,
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddPiggyMetricsSharedDefaults();

        await using var app = builder.Build();
        app.MapPiggyMetricsHealthChecks();

        var httpJson = app.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value;
        Assert.Equal(JsonUnmappedMemberHandling.Skip, httpJson.SerializerOptions.UnmappedMemberHandling);
        Assert.True(httpJson.SerializerOptions.PropertyNameCaseInsensitive);
        Assert.Contains(httpJson.SerializerOptions.Converters, converter => converter is JsonStringEnumConverter);
        Assert.Contains(httpJson.SerializerOptions.Converters, converter => converter is IsoDateTimeOffsetConverter);

        await app.StartAsync();
        var client = app.GetTestServer().CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", await response.Content.ReadAsStringAsync());
        await app.StopAsync();
    }

    private enum ProbeCurrency
    {
        USD,
        EUR,
        RUB
    }

    private sealed class ProbeNamed
    {
        public string Name { get; set; } = "";
    }
}
