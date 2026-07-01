using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace okf;

public sealed record VisualizationStats(int Concepts, int Edges, int Bytes);

public static partial class BundleVisualizer
{
    static readonly Dictionary<string, string> TypePalette = new(StringComparer.Ordinal)
    {
        ["BigQuery Dataset"] = "#8b5cf6",
        ["BigQuery Table"] = "#3b82f6",
        ["Reference"] = "#10b981",
    };

    const string DefaultNodeColor = "#94a3b8";

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static VisualizationStats Generate(string bundleRoot, string outPath, string? bundleName = null)
    {
        // Delegate to the common in-memory graph model (same as `graph` command)
        var graph = GraphBuilder.Build(bundleRoot, bundleName, includeBody: true);
        var name = bundleName ?? graph.Bundle.Name;
        return Generate(graph, outPath, name);
    }

    /// <summary>
    /// Generate visualization from an in-memory KnowledgeGraph (supports both
    /// directory builds and loaded okf.json graphs).
    /// </summary>
    public static VisualizationStats Generate(GraphBuilder.KnowledgeGraph graph, string outPath, string? displayName = null)
    {
        outPath = Path.GetFullPath(outPath);

        var name = displayName ?? graph.Bundle.Name;

        var vizData = BuildVizData(graph);

        var html = ThisAssembly.Resources.Google.viz_template.Text
            .Replace("/*__VIZ_CSS__*/", ThisAssembly.Resources.Google.viz_styles.Text, StringComparison.Ordinal)
            .Replace("/*__VIZ_JS__*/", ThisAssembly.Resources.Google.viz_script.Text, StringComparison.Ordinal)
            .Replace("__BUNDLE_NAME__", JsonSerializer.Serialize(name, JsonOptions), StringComparison.Ordinal)
            .Replace("__BUNDLE_DATA__", JsonSerializer.Serialize(vizData, JsonOptions), StringComparison.Ordinal);

        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllText(outPath, html, Encoding.UTF8);

        return new VisualizationStats(graph.Nodes.Count, graph.Edges.Count, Encoding.UTF8.GetByteCount(html));
    }

    private static GraphData BuildVizData(GraphBuilder.KnowledgeGraph graph)
    {
        var vizNodes = graph.Nodes
            .Select(n => new NodeElement { Data = ToVizNodeData(n) })
            .ToList();

        var vizEdges = graph.Edges
            .Select(e => new EdgeElement
            {
                Data = new EdgeData
                {
                    Id = e.Id,
                    Source = e.Source,
                    Target = e.Target,
                },
            })
            .ToList();

        var bodies = graph.Nodes
            .Where(n => !string.IsNullOrEmpty(n.Body))
            .ToDictionary(n => n.Id, n => n.Body!, StringComparer.Ordinal);

        var types = graph.Nodes
            .Select(n => n.Type)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();

        return new GraphData
        {
            Nodes = vizNodes,
            Edges = vizEdges,
            Bodies = bodies,
            Types = types,
            Palette = TypePalette,
        };
    }

    private static NodeData ToVizNodeData(GraphBuilder.Node n)
    {
        var tags = new List<string>();
        if (n.ExtensionData?.TryGetValue("tags", out var tagElement) == true)
        {
            if (tagElement.ValueKind == JsonValueKind.String)
            {
                var tag = tagElement.GetString();
                if (!string.IsNullOrWhiteSpace(tag))
                    tags.Add(tag);
            }
            else if (tagElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in tagElement.EnumerateArray())
                {
                    var tag = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
                    if (!string.IsNullOrWhiteSpace(tag))
                        tags.Add(tag);
                }
            }
        }

        var color = TypePalette.GetValueOrDefault(n.Type, DefaultNodeColor);
        int bodyLen = n.Body?.Length ?? 0;
        int size = 30 + Math.Min(60, bodyLen / 200);

        return new NodeData
        {
            Id = n.Id,
            Label = string.IsNullOrEmpty(n.Label) ? n.Id : n.Label,
            Type = n.Type,
            Description = GetExtensionString(n.ExtensionData, "description"),
            Resource = GetExtensionString(n.ExtensionData, "resource"),
            Tags = tags,
            Color = color,
            Size = size,
        };
    }

    static string GetExtensionString(Dictionary<string, JsonElement>? extensionData, string key, string def = "")
    {
        if (extensionData is null || !extensionData.TryGetValue(key, out var element))
            return def;

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? def,
            JsonValueKind.Null => def,
            _ => element.ToString(),
        };
    }



    sealed class GraphData
    {
        [JsonPropertyName("nodes")]
        public List<NodeElement> Nodes { get; init; } = [];

        [JsonPropertyName("edges")]
        public List<EdgeElement> Edges { get; init; } = [];

        [JsonPropertyName("bodies")]
        public Dictionary<string, string> Bodies { get; init; } = [];

        [JsonPropertyName("types")]
        public List<string> Types { get; init; } = [];

        [JsonPropertyName("palette")]
        public Dictionary<string, string> Palette { get; init; } = [];
    }

    sealed class NodeElement
    {
        [JsonPropertyName("data")]
        public NodeData Data { get; init; } = new();
    }

    sealed class NodeData
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = "";

        [JsonPropertyName("label")]
        public string Label { get; init; } = "";

        [JsonPropertyName("type")]
        public string Type { get; init; } = "";

        [JsonPropertyName("description")]
        public string Description { get; init; } = "";

        [JsonPropertyName("resource")]
        public string Resource { get; init; } = "";

        [JsonPropertyName("tags")]
        public List<string> Tags { get; init; } = [];

        [JsonPropertyName("color")]
        public string Color { get; init; } = "";

        [JsonPropertyName("size")]
        public int Size { get; init; }
    }

    sealed class EdgeElement
    {
        [JsonPropertyName("data")]
        public EdgeData Data { get; init; } = new();
    }

    sealed class EdgeData
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = "";

        [JsonPropertyName("source")]
        public string Source { get; init; } = "";

        [JsonPropertyName("target")]
        public string Target { get; init; } = "";
    }
}
