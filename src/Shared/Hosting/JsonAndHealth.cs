using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using PiggyMetrics.Shared.Http;
using PiggyMetrics.Shared.Security;
using PiggyMetrics.Shared.Store;

namespace PiggyMetrics.Shared.Hosting;

public static class PiggyMetricsJsonOptions
{
    public const string DateFormat = "yyyy-MM-dd'T'HH:mm:ss.fffK";

    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new IsoDateTimeOffsetConverter());
        return options;
    }
}

public sealed class IsoDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString() ?? throw new JsonException("date missing");
        return DateTimeOffset.Parse(s);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(PiggyMetricsJsonOptions.DateFormat));
    }
}

public static class SharedServiceCollectionExtensions
{
    public static IServiceCollection AddPiggyMetricsSharedDefaults(this IServiceCollection services)
    {
        MongoConventions.Register();
        services.AddHttpClient("userinfo", client =>
        {
            client.Timeout = EdgeHttpDefaults.Timeout;
        });
        services.AddHttpClient("token", client =>
        {
            client.Timeout = EdgeHttpDefaults.Timeout;
        });
        services.AddSingleton<ClientCredentialsTokenCache>();
        services.AddAuthentication(UserInfoBearerOptions.DefaultScheme)
            .AddUserInfoBearer(o =>
            {
                o.UserInfoEndpointUrl = "http://auth-service:5000/uaa/users/current";
            });
        services.AddAuthorization();
        services.AddHealthChecks();
        services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNameCaseInsensitive = true;
            o.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
            o.SerializerOptions.Converters.Clear();
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            o.SerializerOptions.Converters.Add(new IsoDateTimeOffsetConverter());
        });
        return services;
    }

    public static WebApplication MapPiggyMetricsHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        return app;
    }
}
