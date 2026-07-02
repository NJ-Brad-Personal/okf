using System.Text.Json;
using System.Text.Json.Serialization;
using SharpYaml;

namespace okf;

/// <summary>
/// OKF concept frontmatter per spec §4.1: required and recommended fields,
/// with producer extensions captured in <see cref="ExtensionData"/>.
/// </summary>
public class ConceptDocument
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; init; }

    public Dictionary<string, JsonElement>? ExtensionData { get; init; }

    public static bool TryDeserialize(string frontmatterYaml, out ConceptDocument? document)
    {
        try
        {
            var yaml = YamlSerializer.Deserialize<ConceptDocumentYaml>(frontmatterYaml);
            if (yaml is null || string.IsNullOrWhiteSpace(yaml.Type))
            {
                document = null;
                return false;
            }

            document = new ConceptDocument
            {
                Type = yaml.Type,
                Title = yaml.Title,
                Description = yaml.Description,
                Resource = yaml.Resource,
                Tags = yaml.Tags,
                Timestamp = yaml.Timestamp,
                ExtensionData = Converters.ToExtensionData(yaml.ExtensionDataYaml),
            };
        }
        catch
        {
            document = null;
            return false;
        }

        return true;
    }

    // SharpYaml extension data only supports Dictionary<string, object?> values.
    sealed class ConceptDocumentYaml : ConceptDocument
    {
        [JsonExtensionData]
        public Dictionary<string, object?>? ExtensionDataYaml { get; set; }
    }
}