using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PiggyMetrics.Parity.Tests;

public sealed record ParityRow
{
    public required string Contract { get; init; }
    public required string Method { get; init; }
    public required string Path { get; init; }
    public required string Auth { get; init; }
    public required int PolicyStatus { get; init; }
    public required int SourceStatus { get; init; }
    public required int DestinationStatus { get; init; }
    public required bool FieldMatch { get; init; }
    public required string Detail { get; init; }

    public string Route => Method + " " + Path;

    public bool StatusMatch => SourceStatus == DestinationStatus;

    public bool PolicyMatch => SourceStatus == PolicyStatus && DestinationStatus == PolicyStatus;

    public bool Parity => StatusMatch && PolicyMatch && FieldMatch;

    public override string ToString() =>
        Contract + " " + Route + " auth=" + Auth + " policy=" + PolicyStatus + " source=" + SourceStatus + " dest=" + DestinationStatus + " " + Detail;
}

public sealed class ParityReport
{
    public ParityReport(IReadOnlyList<ParityRow> rows)
    {
        Rows = rows;
    }

    public string Plan { get; } = "P7";

    public IReadOnlyList<ParityRow> Rows { get; }

    public IEnumerable<IGrouping<string, ParityRow>> ByRoute => Rows.GroupBy(row => row.Route);

    public string ToJson()
    {
        var payload = new
        {
            plan = Plan,
            routes = ByRoute.Select(group => new
            {
                route = group.Key,
                parity = group.All(row => row.Parity),
                rows = group.Select(row => new
                {
                    contract = row.Contract,
                    auth = row.Auth,
                    policyStatus = row.PolicyStatus,
                    sourceStatus = row.SourceStatus,
                    destinationStatus = row.DestinationStatus,
                    parity = row.Parity,
                    detail = row.Detail
                })
            })
        };
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}

public static class ParityRunner
{
    private static readonly Regex Timestamp = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}(Z|[+-]\d{2}:\d{2})$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static async Task<ParityReport> RunAsync()
    {
        var rows = new List<ParityRow>();
        await using var source = await SourceSide.StartAsync();
        foreach (var group in ContractCatalog.All.GroupBy(item => item.Id + "|" + item.Service))
        {
            var service = group.First().Service;
            source.ResetSeed();
            if (service == ServiceKind.Auth)
            {
                await source.IssueSeedTokensAsync();
            }

            await using var destination = await DestinationSide.StartAsync(service);
            foreach (var contract in group)
            {
                foreach (var auth in new[] { AuthKind.None, AuthKind.User, AuthKind.Server })
                {
                    rows.Add(await ReplayAsync(source, destination, contract, auth));
                }
            }
        }

        return new ParityReport(rows);
    }

    public static string ReportPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PiggyMetricsDotNet.sln")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("solution root not found");
        }

        return Path.Combine(dir.FullName, "tests", "Parity.Tests", "parity-report.json");
    }

    private static async Task<ParityRow> ReplayAsync(SourceSide source, DestinationSide destination, ContractCase contract, AuthKind auth)
    {
        var policy = contract.Policy[auth];
        var json = contract.Json?.Invoke(auth);
        var sourceBearer = AccessToken(source, null, contract.Service, auth);
        var destinationBearer = AccessToken(null, destination, contract.Service, auth);
        try
        {
            var sourceResponse = await SendAsync(source.Client, contract.Method, contract.Path, json, contract.Form, sourceBearer);
            var destinationResponse = await SendAsync(destination.Client, contract.Method, contract.Path, json, contract.Form, destinationBearer);
            string? detail = sourceResponse.Status == destinationResponse.Status
                ? CompareFields(contract.Fields(auth), sourceResponse.Body, destinationResponse.Body)
                : "status mismatch";
            return new ParityRow
            {
                Contract = contract.Id,
                Method = contract.Method,
                Path = contract.Path,
                Auth = auth.ToString().ToLowerInvariant(),
                PolicyStatus = policy,
                SourceStatus = sourceResponse.Status,
                DestinationStatus = destinationResponse.Status,
                FieldMatch = detail is null,
                Detail = detail ?? "match"
            };
        }
        catch (Exception ex)
        {
            return new ParityRow
            {
                Contract = contract.Id,
                Method = contract.Method,
                Path = contract.Path,
                Auth = auth.ToString().ToLowerInvariant(),
                PolicyStatus = policy,
                SourceStatus = 0,
                DestinationStatus = 0,
                FieldMatch = false,
                Detail = ex.GetType().Name + ": " + ex.Message
            };
        }
    }

    private static string? AccessToken(SourceSide? source, DestinationSide? destination, ServiceKind service, AuthKind auth)
    {
        if (auth == AuthKind.None)
        {
            return null;
        }

        if (service != ServiceKind.Auth)
        {
            return auth == AuthKind.User ? ExternalStubCatalog.UserToken : ExternalStubCatalog.ServerToken;
        }

        if (source is not null)
        {
            return auth == AuthKind.User ? source.UserAccess : source.ServerAccess;
        }

        return auth == AuthKind.User ? destination!.UserAccess : destination!.ServerAccess;
    }

    private static async Task<(int Status, string Body)> SendAsync(
        HttpClient client,
        string method,
        string path,
        string? json,
        IReadOnlyDictionary<string, string>? form,
        string? bearer)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (json is not null)
        {
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        }
        else if (form is not null)
        {
            request.Content = new FormUrlEncodedContent(form.ToDictionary(pair => pair.Key, pair => pair.Value));
        }

        if (!string.IsNullOrEmpty(bearer))
        {
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearer);
        }

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return ((int)response.StatusCode, body);
    }

    private static string? CompareFields(IReadOnlyList<FieldExpect> fields, string sourceBody, string destinationBody)
    {
        foreach (var field in fields)
        {
            var mismatch = CompareField(field, sourceBody, destinationBody);
            if (mismatch is not null)
            {
                return mismatch + " source=" + Trim(sourceBody) + " dest=" + Trim(destinationBody);
            }
        }

        return null;
    }

    private static string? CompareField(FieldExpect field, string sourceBody, string destinationBody)
    {
        switch (field.Mode)
        {
            case FieldMode.EmptyBody:
                return string.IsNullOrWhiteSpace(sourceBody) && string.IsNullOrWhiteSpace(destinationBody)
                    ? null
                    : "expected empty body";
            case FieldMode.JsonNull:
                return IsJsonNull(sourceBody) && IsJsonNull(destinationBody) ? null : "expected JSON null";
            case FieldMode.EmptyArray:
                return IsEmptyArray(sourceBody) && IsEmptyArray(destinationBody) ? null : "expected empty array";
        }

        JsonDocument? sourceDoc;
        JsonDocument? destinationDoc;
        try
        {
            sourceDoc = Parse(sourceBody);
            destinationDoc = Parse(destinationBody);
        }
        catch (JsonException ex)
        {
            return "json: " + ex.Message;
        }

        using (sourceDoc)
        using (destinationDoc)
        {
            if (sourceDoc is null || destinationDoc is null)
            {
                return "missing json for " + field.Path;
            }

            if (!TrySelect(sourceDoc.RootElement, field.Path!, out var sourceValue))
            {
                return "source missing " + field.Path;
            }

            if (!TrySelect(destinationDoc.RootElement, field.Path!, out var destinationValue))
            {
                return "destination missing " + field.Path;
            }

            if (field.Mode == FieldMode.Timestamp)
            {
                var sourceText = sourceValue.GetString();
                var destinationText = destinationValue.GetString();
                if (sourceText is null || !Timestamp.IsMatch(sourceText) || destinationText is null || !Timestamp.IsMatch(destinationText))
                {
                    return field.Path + " timestamp source=" + sourceText + " dest=" + destinationText;
                }

                return null;
            }

            if (field.Mode == FieldMode.StringSet)
            {
                var sourceSet = Strings(sourceValue);
                var destinationSet = Strings(destinationValue);
                if (!sourceSet.SetEquals(destinationSet))
                {
                    return field.Path + " set mismatch";
                }

                if (field.Anchor is not null && !sourceSet.SetEquals(field.Anchor.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
                {
                    return field.Path + " anchor " + field.Anchor;
                }

                return null;
            }

            if (!Same(sourceValue, destinationValue))
            {
                return field.Path + " source=" + sourceValue.GetRawText() + " dest=" + destinationValue.GetRawText();
            }

            if (field.Anchor is not null && !MatchesAnchor(destinationValue, field.Anchor))
            {
                return field.Path + " anchor " + field.Anchor + " actual=" + destinationValue.GetRawText();
            }

            return null;
        }
    }

    private static bool MatchesAnchor(JsonElement value, string anchor)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() == anchor,
            JsonValueKind.Number => value.GetDecimal() == decimal.Parse(anchor, CultureInfo.InvariantCulture),
            JsonValueKind.True => anchor == "true",
            JsonValueKind.False => anchor == "false",
            JsonValueKind.Null => anchor == "null",
            _ => false
        };
    }

    private static bool Same(JsonElement left, JsonElement right)
    {
        if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number)
        {
            return left.GetDecimal() == right.GetDecimal();
        }

        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        return left.ValueKind switch
        {
            JsonValueKind.String => left.GetString() == right.GetString(),
            JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => true,
            _ => left.GetRawText() == right.GetRawText()
        };
    }

    private static HashSet<string> Strings(JsonElement value) =>
        value.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToHashSet(StringComparer.Ordinal);

    private static bool IsJsonNull(string body) => body.Trim() == "null";

    private static bool IsEmptyArray(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Array && document.RootElement.GetArrayLength() == 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static JsonDocument? Parse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        return JsonDocument.Parse(body);
    }

    private static bool TrySelect(JsonElement element, string path, out JsonElement selected)
    {
        selected = element;
        var index = 0;
        while (index < path.Length)
        {
            if (path[index] == '.')
            {
                index++;
            }

            if (index < path.Length && path[index] == '[')
            {
                var end = path.IndexOf(']', index);
                var item = int.Parse(path[(index + 1)..end], CultureInfo.InvariantCulture);
                if (selected.ValueKind != JsonValueKind.Array || item >= selected.GetArrayLength())
                {
                    return false;
                }

                selected = selected[item];
                index = end + 1;
                continue;
            }

            var start = index;
            while (index < path.Length && path[index] != '.' && path[index] != '[')
            {
                index++;
            }

            var name = path[start..index];
            if (name.Length == 0)
            {
                continue;
            }

            if (selected.ValueKind != JsonValueKind.Object || !selected.TryGetProperty(name, out var next))
            {
                return false;
            }

            selected = next;
        }

        return true;
    }

    private static string Trim(string body)
    {
        var text = body.Replace("\n", " ").Replace("\r", "");
        return text.Length <= 280 ? text : text[..280];
    }
}
