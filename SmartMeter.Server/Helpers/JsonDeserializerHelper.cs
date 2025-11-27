using System.Text.Json;

namespace SmartMeter.Server.Helpers;

public static class JsonDeserializerHelper
{
    public static bool TryDeserialize<T>(string json, JsonSerializerOptions options, out T? result)
    {
        try
        {
            result = JsonSerializer.Deserialize<T>(json, options);
            return result is not null;
        }
        catch (JsonException)
        {
            result = default;
            return false;
        }
    }
}