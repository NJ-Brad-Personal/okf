using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace okf;

public static partial class GraphBuilder
{
    const string IndexName = "index.md";
    const string LogName = "log.md";

    static readonly TextInfo TitleCasing = CultureInfo.CurrentCulture.TextInfo;

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    public sealed record KnowledgeGraph(
        [property: JsonPropertyName("bundle")] Bundle Bundle,
        [property: JsonPropertyName("nodes")] List<Node> Nodes,
        [property: JsonPropertyName("edges")] List<Edge> Edges)
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; init; }
    }

    public sealed record Bundle(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("root")] string Root,
        [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
        [property: JsonPropertyName("concepts")] int Concepts)
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; init; }
    }

    public sealed record Node(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("type")] string Type)
    {
        [JsonPropertyName("path")]
        public required string Path { get; init; }

        [JsonPropertyName("degree")]
        public required int Degree { get; init; }

        [JsonPropertyName("in")]
        public required int In { get; init; }

        [JsonPropertyName("out")]
        public required int Out { get; init; }

        [JsonPropertyName("label")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Label { get; init; }

        [JsonPropertyName("body")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Body { get; init; }

        [JsonPropertyName("weight")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Weight { get; init; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; init; }
    }

    public sealed record Edge(
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("target")] string Target,
        [property: JsonPropertyName("id")] string Id = "")
    {
        [JsonPropertyName("label")]
        public string? Label { get; init; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; init; }
    }

    /// <summary>
    /// Loads a previously generated knowledge graph from its JSON file.
    /// </summary>
    public static KnowledgeGraph Load(string graphJsonPath)
    {
        var fullPath = Path.GetFullPath(graphJsonPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Graph file not found: {fullPath}");
        }

        var json = File.ReadAllText(fullPath);
        // Strip UTF-8 BOM if present (some older files)
        if (json.Length > 0 && json[0] == '\uFEFF')
            json = json[1..];

        var graph = JsonSerializer.Deserialize<KnowledgeGraph>(json, JsonOptions);
        if (graph is null)
            throw new InvalidDataException($"Failed to deserialize graph from {fullPath}");

        return EnsureNodeWeights(EnsureEdgeIds(graph));
    }

    static KnowledgeGraph EnsureEdgeIds(KnowledgeGraph graph)
    {
        if (graph.Edges.All(e => !string.IsNullOrWhiteSpace(e.Id)))
            return graph;

        var conceptIds = graph.Nodes
            .Select(n => n.Id)
            .Concat(graph.Edges.SelectMany(e => new[] { e.Source, e.Target }))
            .Distinct(StringComparer.Ordinal);
        var conceptAbbrs = ShortIds.ComputeConceptAbbreviations(conceptIds);

        var edges = graph.Edges
            .Select(e => string.IsNullOrWhiteSpace(e.Id)
                ? e with { Id = FormatEdgeId(conceptAbbrs, e.Source, e.Target) }
                : e)
            .ToList();

        return graph with { Edges = edges };
    }

    static KnowledgeGraph EnsureNodeWeights(KnowledgeGraph graph)
    {
        if (graph.Nodes.All(n => n.Weight.HasValue))
            return graph;

        var weights = PageRank.Compute(graph.Nodes, graph.Edges);
        var nodes = graph.Nodes
            .Select(n => n.Weight.HasValue ? n : n with { Weight = weights[n.Id] })
            .ToList();

        return graph with { Nodes = nodes };
    }

    static Dictionary<string, JsonElement>? ToExtensionData(IReadOnlyDictionary<string, object?> frontmatter)
    {
        var extensionData = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (key, value) in frontmatter)
        {
            if (string.Equals(key, "type", StringComparison.Ordinal) ||
                string.Equals(key, "label", StringComparison.Ordinal) ||
                value is null)
            {
                continue;
            }

            extensionData[key] = JsonSerializer.SerializeToElement(value, JsonOptions);
        }

        return extensionData.Count > 0 ? extensionData : null;
    }

    static string FormatEdgeId(IReadOnlyDictionary<string, string> conceptAbbrs, string source, string target)
        => $"{conceptAbbrs[source]}_{conceptAbbrs[target]}";

    /// <summary>
    /// Builds the knowledge graph for a bundle and returns the serializable structure.
    /// </summary>
    public static KnowledgeGraph Build(string bundleRoot, string? bundleName = null, bool includeBody = false)
    {
        bundleRoot = Path.GetFullPath(bundleRoot);

        if (!Directory.Exists(bundleRoot))
        {
            throw new DirectoryNotFoundException($"Bundle directory not found: {bundleRoot}");
        }

        var concepts = WalkConcepts(bundleRoot, includeBody);
        var graph = BuildGraph(concepts, bundleRoot, bundleName);
        return graph;
    }

    /// <summary>
    /// Builds the graph and writes it as pretty JSON to the given path. Returns basic stats.
    /// </summary>
    public static (int Concepts, int Edges) Generate(string bundleRoot, string outPath, string? bundleName = null, bool includeBody = false)
    {
        var graph = Build(bundleRoot, bundleName, includeBody);

        var outDir = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(outDir))
        {
            Directory.CreateDirectory(outDir);
        }
        var json = JsonSerializer.Serialize(graph, JsonOptions);
        File.WriteAllText(outPath, json, Encoding.UTF8);

        return (graph.Nodes.Count, graph.Edges.Count);
    }

    static List<Concept> WalkConcepts(string bundleRoot, bool includeBody)
    {
        var concepts = new List<Concept>();
        var bundleRootFull = Path.GetFullPath(bundleRoot);

        foreach (var absolutePath in Directory.EnumerateFiles(bundleRoot, "*.md", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(absolutePath);
            if (fileName.Equals(IndexName, StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals(LogName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(bundleRootFull, absolutePath).Replace('\\', '/');
            var conceptId = Path.ChangeExtension(relativePath, null)!.Replace('\\', '/');

            var text = File.ReadAllText(absolutePath);

            if (!OKFDocument.TryParse(text, out var document, out _))
            {
                continue;
            }

            var frontmatter = document!.Frontmatter;
            var type = OKFDocument.GetTypeValue(frontmatter);
            if (string.IsNullOrWhiteSpace(type))
            {
                // Invalid OKF document per spec - skip
                continue;
            }

            var title = GetStringValue(frontmatter, "title");
            var label = GetStringValue(frontmatter, "label");
            if (string.IsNullOrWhiteSpace(label))
            {
                label = null;
            }

            concepts.Add(new Concept(
                conceptId,
                relativePath,
                type!,
                title ?? conceptId,
                label,
                ToExtensionData(frontmatter),
                includeBody ? document.Body : null,
                ExtractLinks(document.Body, relativePath, bundleRootFull)));
        }

        return concepts;
    }

    static KnowledgeGraph BuildGraph(List<Concept> concepts, string bundleRoot, string? bundleName)
    {
        var name = bundleName ?? new DirectoryInfo(bundleRoot).Name;
        var ids = concepts.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var idToTitle = concepts.ToDictionary(c => c.Id, c => c.Title, StringComparer.Ordinal);

        var conceptAbbrs = ShortIds.ComputeConceptAbbreviations(concepts.Select(c => c.Id));

        var nodes = new List<Node>();
        var edges = new List<Edge>();
        var seenEdges = new HashSet<(string Source, string Target)>(comparer: null);

        // Build nodes first (without degrees)
        var nodeLookup = new Dictionary<string, Node>(StringComparer.Ordinal);

        foreach (var c in concepts.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var node = new Node(c.Id, c.Type)
            {
                Path = c.Path,
                Degree = 0,
                In = 0,
                Out = 0,
                ExtensionData = c.ExtensionData,
                Label = c.Label,
                Body = c.Body,
            };

            nodes.Add(node);
            nodeLookup[c.Id] = node;
        }

        // Resolve links and build edges
        foreach (var c in concepts)
        {
            foreach (var (linkText, rawTarget) in c.LinksTo)
            {
                if (!MarkdownLinks.IsInternalLink(rawTarget))
                {
                    continue;
                }

                if (!MarkdownLinks.TryResolve(rawTarget, c.Path, bundleRoot, out var resolved))
                {
                    continue;
                }

                var targetId = NormalizeToConceptId(resolved);
                if (targetId == c.Id || !ids.Contains(targetId))
                {
                    continue;
                }

                var key = (c.Id, targetId);
                if (!seenEdges.Add(key))
                {
                    continue;
                }

                string? label = !string.IsNullOrWhiteSpace(linkText)
                    ? linkText
                    : (idToTitle.TryGetValue(targetId, out var t) && !string.IsNullOrEmpty(t) ? t : targetId);

                edges.Add(new Edge(c.Id, targetId, FormatEdgeId(conceptAbbrs, c.Id, targetId))
                {
                    Label = label,
                });
            }
        }

        var indexLabels = LoadIndexLinkLabels(bundleRoot, ids);
        var incomingLabels = CollectIncomingLinkTexts(concepts, ids, bundleRoot);
        var derivedLabels = ResolveDerivedLabels(concepts, indexLabels, incomingLabels);

        // Compute degrees
        var outDeg = new Dictionary<string, int>(StringComparer.Ordinal);
        var inDeg = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var e in edges)
        {
            outDeg[e.Source] = outDeg.GetValueOrDefault(e.Source) + 1;
            inDeg[e.Target] = inDeg.GetValueOrDefault(e.Target) + 1;
        }

        // Update nodes with degrees and derived labels (records are immutable here)
        var finalNodes = new List<Node>(nodes.Count);
        foreach (var n in nodes)
        {
            var ins = inDeg.GetValueOrDefault(n.Id);
            var outs = outDeg.GetValueOrDefault(n.Id);
            var label = n.Label ?? derivedLabels.GetValueOrDefault(n.Id);
            finalNodes.Add(n with { In = ins, Out = outs, Degree = ins + outs, Label = label });
        }

        var weights = PageRank.Compute(finalNodes, edges);
        finalNodes = finalNodes
            .Select(n => n with { Weight = weights[n.Id] })
            .ToList();

        var bundle = new Bundle(
            name,
            bundleRoot.Replace('\\', '/'),
            DateTimeOffset.UtcNow,
            finalNodes.Count);

        return new KnowledgeGraph(bundle, finalNodes, edges);
    }

    static string NormalizeToConceptId(string resolvedRelative)
    {
        var id = resolvedRelative.Replace('\\', '/');
        if (id.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            id = id[..^3];
        }

        if (id.EndsWith("/index", StringComparison.OrdinalIgnoreCase))
        {
            id = id[..^6];
        }

        id = id.TrimEnd('/');
        return id;
    }

    static Dictionary<string, string> LoadIndexLinkLabels(string bundleRoot, HashSet<string> conceptIds)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        var bundleRootFull = Path.GetFullPath(bundleRoot);

        foreach (var absolutePath in Directory.EnumerateFiles(bundleRootFull, IndexName, SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(bundleRootFull, absolutePath).Replace('\\', '/');
            var text = File.ReadAllText(absolutePath);
            var body = GetIndexBody(relativePath, text);

            foreach (var (linkText, rawTarget, _) in MarkdownLinks.ExtractWithText(body))
            {
                if (string.IsNullOrWhiteSpace(linkText) || !MarkdownLinks.IsInternalLink(rawTarget))
                {
                    continue;
                }

                if (!MarkdownLinks.TryResolve(rawTarget, relativePath, bundleRootFull, out var resolved))
                {
                    continue;
                }

                var targetId = NormalizeToConceptId(resolved);
                if (!conceptIds.Contains(targetId) || labels.ContainsKey(targetId))
                {
                    continue;
                }

                labels[targetId] = linkText;
            }
        }

        return labels;
    }

    static string GetIndexBody(string relativePath, string text)
    {
        var isBundleRoot = relativePath.Equals(IndexName, StringComparison.OrdinalIgnoreCase);
        if (isBundleRoot
            && OKFDocument.HasFrontmatterBlock(text)
            && OKFDocument.TryParse(text, out var document, out _))
        {
            return document!.Body;
        }

        return text;
    }

    static Dictionary<string, string> CollectIncomingLinkTexts(
        List<Concept> concepts,
        HashSet<string> conceptIds,
        string bundleRoot)
    {
        var incoming = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var concept in concepts)
        {
            foreach (var (linkText, rawTarget) in concept.LinksTo)
            {
                if (string.IsNullOrWhiteSpace(linkText) || !MarkdownLinks.IsInternalLink(rawTarget))
                {
                    continue;
                }

                if (!MarkdownLinks.TryResolve(rawTarget, concept.Path, bundleRoot, out var resolved))
                {
                    continue;
                }

                var targetId = NormalizeToConceptId(resolved);
                if (targetId == concept.Id || !conceptIds.Contains(targetId))
                {
                    continue;
                }

                if (!incoming.TryGetValue(targetId, out var texts))
                {
                    texts = [];
                    incoming[targetId] = texts;
                }

                texts.Add(linkText);
            }
        }

        return incoming.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value
                .OrderBy(t => t.Length)
                .ThenBy(t => t, StringComparer.Ordinal)
                .First(),
            StringComparer.Ordinal);
    }

    static Dictionary<string, string> ResolveDerivedLabels(
        List<Concept> concepts,
        Dictionary<string, string> indexLabels,
        Dictionary<string, string> incomingLabels)
    {
        var needsFallback = concepts
            .Where(c => string.IsNullOrWhiteSpace(c.Label))
            .ToList();

        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        var idFallbackIds = new List<string>();

        foreach (var concept in needsFallback)
        {
            if (indexLabels.TryGetValue(concept.Id, out var indexLabel))
            {
                resolved[concept.Id] = indexLabel;
                continue;
            }

            if (incomingLabels.TryGetValue(concept.Id, out var incomingLabel))
            {
                resolved[concept.Id] = incomingLabel;
                continue;
            }

            idFallbackIds.Add(concept.Id);
        }

        foreach (var (conceptId, label) in DisambiguateIdFallbackLabels(idFallbackIds))
        {
            resolved[conceptId] = label;
        }

        return resolved;
    }

    static IEnumerable<(string ConceptId, string Label)> DisambiguateIdFallbackLabels(IReadOnlyList<string> conceptIds)
    {
        if (conceptIds.Count == 0)
        {
            yield break;
        }

        var baseLabels = conceptIds.ToDictionary(
            id => id,
            id => TitleCaseSegment(GetLastSegment(id)),
            StringComparer.Ordinal);

        var groups = baseLabels
            .GroupBy(kvp => kvp.Value, StringComparer.Ordinal)
            .ToList();

        foreach (var group in groups)
        {
            var members = group.Select(kvp => kvp.Key).ToList();
            if (members.Count == 1)
            {
                yield return (members[0], group.Key);
                continue;
            }

            var parentSegments = members.ToDictionary(
                id => id,
                GetParentSegments,
                StringComparer.Ordinal);

            var depth = 1;
            string[]? finalLabels = null;

            while (depth <= members.Max(id => parentSegments[id].Length))
            {
                var candidateLabels = members.ToDictionary(
                    id => id,
                    id => FormatIdFallbackLabel(group.Key, parentSegments[id], depth),
                    StringComparer.Ordinal);

                if (candidateLabels.Values.Distinct(StringComparer.Ordinal).Count() == members.Count)
                {
                    finalLabels = members.Select(id => candidateLabels[id]).ToArray();
                    break;
                }

                depth++;
            }

            finalLabels ??= members
                .Select(id => FormatIdFallbackLabel(group.Key, parentSegments[id], parentSegments[id].Length))
                .ToArray();

            for (var i = 0; i < members.Count; i++)
            {
                yield return (members[i], finalLabels[i]);
            }
        }
    }

    static string FormatIdFallbackLabel(string baseLabel, string[] parentSegments, int depth)
    {
        if (parentSegments.Length == 0)
        {
            return baseLabel;
        }

        var count = Math.Min(depth, parentSegments.Length);
        var parents = parentSegments[^count..]
            .Select(TitleCaseSegment)
            .ToArray();

        return $"{baseLabel} ({string.Join(", ", parents)})";
    }

    static string[] GetParentSegments(string conceptId)
        => conceptId.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[..^1];

    static string GetLastSegment(string conceptId)
    {
        var segments = conceptId.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length > 0 ? segments[^1] : conceptId;
    }

    static string TitleCaseSegment(string segment)
        => string.IsNullOrEmpty(segment)
            ? segment
            : TitleCasing.ToTitleCase(segment);

    static List<(string Text, string Target)> ExtractLinks(string body, string sourceRelativePath, string bundleRoot)
    {
        var links = new List<(string, string)>();

        foreach (var (text, target, _) in MarkdownLinks.ExtractWithText(body))
        {
            if (!MarkdownLinks.IsInternalLink(target))
            {
                continue;
            }

            links.Add((text, target));
        }

        return links;
    }

    static string? GetStringValue(IReadOnlyDictionary<string, object?> frontmatter, string key)
    {
        if (!frontmatter.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string s => s,
            _ => value.ToString(),
        };
    }

    // Internal concept representation
    sealed record Concept(
        string Id,
        string Path,
        string Type,
        string Title,
        string? Label,
        Dictionary<string, JsonElement>? ExtensionData,
        string? Body,
        List<(string Text, string Target)> LinksTo);
}
