using System.Text.Encodings.Web;
using System.Text.Json;

namespace Devlooped;

static class Converters
{
    static readonly JsonSerializerOptions JsonElementOptions = new()
    {
        PropertyNamingPolicy = null,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static JsonElement ToJsonElement<T>(T value)
        => JsonSerializer.SerializeToElement(value, JsonElementOptions);

    public static Dictionary<string, JsonElement>? ToExtensionData(IReadOnlyDictionary<string, object?>? extensionData)
    {
        if (extensionData is null || extensionData.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (key, value) in extensionData)
        {
            if (value is null)
            {
                continue;
            }

            result[key] = ToJsonElement(value);
        }

        return result.Count > 0 ? result : null;
    }
}