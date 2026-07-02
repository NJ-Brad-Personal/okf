using System.Text.Json.Serialization;

namespace okf;

// SharpYaml extension data only supports Dictionary<string, object> values.
internal sealed class ConceptDocumentYaml
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("resource")]
    public string? Resource { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? ExtensionDataYaml { get; set; }
}