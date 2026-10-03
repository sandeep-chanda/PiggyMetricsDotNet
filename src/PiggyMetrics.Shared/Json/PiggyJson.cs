using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PiggyMetrics.Shared.Json;

/// <summary>JSON behaviour of the source: Jackson 2.9 under Spring Boot 2.0.3 defaults (decision D-011).</summary>
public static class PiggyJson
{
    public static JsonSerializerOptions Default { get; } = CreateOptions();

    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        Configure(options);
        return options;
    }

    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = false;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
        options.NumberHandling = JsonNumberHandling.AllowReadingFromString;
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new JacksonDateTimeConverter());
    }
}

/// <summary>
/// Writes and reads DateTime the way Jackson 2.9 does with WRITE_DATES_AS_TIMESTAMPS disabled:
/// yyyy-MM-dd'T'HH:mm:ss.SSSZ, offset rendered without a colon.
/// </summary>
public sealed class JacksonDateTimeConverter : JsonConverter<DateTime>
{
    public const string WriteFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'+0000'";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64()).UtcDateTime;
        }

        var text = reader.GetString();
        return string.IsNullOrWhiteSpace(text)
            ? throw new JsonException("Expected an ISO-8601 date-time string or epoch milliseconds.")
            : Parse(text);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var utc = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
        writer.WriteStringValue(utc.ToString(WriteFormat, CultureInfo.InvariantCulture));
    }

    public static DateTime Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return DateTimeOffset.Parse(
            NormalizeOffset(text.Trim()),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal).UtcDateTime;
    }

    // "+0000" and "-0500" become "+00:00" and "-05:00"; "Z" and "+00:00" are left alone.
    private static string NormalizeOffset(string text)
    {
        if (text.Length < 5 || (text[^5] != '+' && text[^5] != '-'))
        {
            return text;
        }

        for (var index = text.Length - 4; index < text.Length; index++)
        {
            if (!char.IsAsciiDigit(text[index]))
            {
                return text;
            }
        }

        return string.Concat(text.AsSpan(0, text.Length - 2), ":".AsSpan(), text.AsSpan(text.Length - 2));
    }
}
