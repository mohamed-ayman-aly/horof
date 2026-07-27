using System.Text.Json;
using System.Text.Json.Serialization;

namespace horof.Services.Network;

public static class LanMethods
{
    public const string JoinRoom = "JoinRoom";
    public const string SetReady = "SetReady";
    public const string SelectHex = "SelectHex";
    public const string Buzz = "Buzz";
    public const string HostJudge = "HostJudge";
    public const string GetSnapshot = "GetSnapshot";
    public const string SessionUpdated = "SessionUpdated";
}

public sealed class LanRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("args")]
    public JsonElement[] Args { get; set; } = [];
}

public sealed class LanResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("result")]
    public JsonElement? Result { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class LanPush
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("args")]
    public JsonElement[] Args { get; set; } = [];
}

public static class LanJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string SerializeRequest(string id, string method, params object?[] args)
    {
        var request = new LanRequest
        {
            Id = id,
            Method = method,
            Args = args.Select(a => JsonSerializer.SerializeToElement(a, Options)).ToArray()
        };
        return JsonSerializer.Serialize(request, Options);
    }

    public static string SerializeResponse(string id, bool ok, object? result = null, string? error = null)
    {
        var response = new LanResponse
        {
            Id = id,
            Ok = ok,
            Error = error,
            Result = result is null ? null : JsonSerializer.SerializeToElement(result, Options)
        };
        return JsonSerializer.Serialize(response, Options);
    }

    public static string SerializePush(string method, params object?[] args)
    {
        var push = new LanPush
        {
            Method = method,
            Args = args.Select(a => JsonSerializer.SerializeToElement(a, Options)).ToArray()
        };
        return JsonSerializer.Serialize(push, Options);
    }

    public static bool TryParseLine(string line, out LanRequest? request, out LanResponse? response, out LanPush? push)
    {
        request = null;
        response = null;
        push = null;

        if (string.IsNullOrWhiteSpace(line))
            return false;

        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        if (root.TryGetProperty("ok", out _))
        {
            response = JsonSerializer.Deserialize<LanResponse>(line, Options);
            return response is not null;
        }

        if (root.TryGetProperty("id", out _) && root.TryGetProperty("method", out _))
        {
            request = JsonSerializer.Deserialize<LanRequest>(line, Options);
            return request is not null;
        }

        if (root.TryGetProperty("method", out _))
        {
            push = JsonSerializer.Deserialize<LanPush>(line, Options);
            return push is not null;
        }

        return false;
    }

    public static T? DeserializeResult<T>(JsonElement? result)
    {
        if (result is null || result.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return default;

        return result.Value.Deserialize<T>(Options);
    }
}
