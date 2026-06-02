using System.Text;
using System.Text.Json;

namespace PenguinTwitchBot.TwitchApi.Helix;

internal static class HelixJson
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions JsonOptionsIgnoreNulls = new(JsonOptions)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    internal static StringContent CreateJsonContent<T>(T body)
        => CreateJsonContent(body, ignoreNullValues: false);

    internal static StringContent CreateJsonContent<T>(T body, bool ignoreNullValues)
    {
        var json = JsonSerializer.Serialize(body, ignoreNullValues ? JsonOptionsIgnoreNulls : JsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    internal static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        if (stream.CanSeek && stream.Length == 0)
        {
            return default;
        }

        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
    }
}