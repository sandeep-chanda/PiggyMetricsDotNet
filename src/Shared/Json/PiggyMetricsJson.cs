using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PiggyMetrics.Shared.Json;

// Reads and writes exactly as the source stack does: Spring Boot 2.0.3 disables
// SerializationFeature.WRITE_DATES_AS_TIMESTAMPS, so Jackson 2.9.6 falls back to StdDateFormat, whose pattern
// is yyyy-MM-dd'T'HH:mm:ss.SSSZ rendered in UTC with a colon-less offset (2018-06-01T12:00:00.000+0000).
public sealed class JacksonDateTimeConverter : JsonConverter<DateTime>
{
	public const string WriteFormat = "yyyy-MM-dd'T'HH:mm:ss.fff";
	public const string WriteOffset = "+0000";

	public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Number)
		{
			return DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64()).UtcDateTime;
		}

		var text = reader.GetString();
		if (string.IsNullOrWhiteSpace(text))
		{
			throw new JsonException("A date value must not be empty.");
		}

		if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epochMilliseconds))
		{
			return DateTimeOffset.FromUnixTimeMilliseconds(epochMilliseconds).UtcDateTime;
		}

		return DateTimeOffset.Parse(
			text,
			CultureInfo.InvariantCulture,
			DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal).UtcDateTime;
	}

	public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
	{
		var utc = value.Kind == DateTimeKind.Unspecified
			? DateTime.SpecifyKind(value, DateTimeKind.Utc)
			: value.ToUniversalTime();
		writer.WriteStringValue(utc.ToString(WriteFormat, CultureInfo.InvariantCulture) + WriteOffset);
	}
}

// The single JSON contract for every PiggyMetrics .NET service: camelCase names, enum values as their declared
// names, unknown incoming members skipped, Jackson's date rendering.
public static class PiggyMetricsJson
{
	public static readonly JsonSerializerOptions Options = CreateOptions();

	public static JsonSerializerOptions CreateOptions()
	{
		var options = new JsonSerializerOptions();
		Configure(options);
		options.MakeReadOnly();
		return options;
	}

	public static void Configure(JsonSerializerOptions options)
	{
		options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
		options.PropertyNameCaseInsensitive = false;
		options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
		options.NumberHandling = JsonNumberHandling.AllowReadingFromString;
		options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
		options.Converters.Add(new JacksonDateTimeConverter());
	}
}
