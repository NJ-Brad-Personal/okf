using System.Text.Json;
using okf;
using Xunit;

namespace Tests;

public class GraphBuilderTests
{
    static string FixturePath(string name)
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Fixtures", name));

    [Fact]
    public void Generates_expected_shape_and_counts_for_valid_bundle()
    {
        var graph = GraphBuilder.Build(FixturePath("valid"));

        Assert.Equal(2, graph.Nodes.Count);
        Assert.Equal(1, graph.Edges.Count); // absolute /tables/customers link from orders
        Assert.Equal("valid", graph.Bundle.Name); // dir name of fixture (lowercase)
        Assert.Equal(2, graph.Bundle.Concepts);
        Assert.True(graph.Bundle.Timestamp != default);
        Assert.NotNull(graph.Bundle.Root);
        Assert.Contains("valid", graph.Bundle.Root.Replace('\\', '/'));
    }

    [Fact]
    public void Absolute_link_produces_edge_and_correct_degrees()
    {
        var graph = GraphBuilder.Build(FixturePath("valid"));

        var orders = graph.Nodes.First(n => n.Id == "tables/orders");
        var customers = graph.Nodes.First(n => n.Id == "tables/customers");

        Assert.Equal(1, orders.Out);
        Assert.Equal(0, orders.In);
        Assert.Equal(1, orders.Degree);

        Assert.Equal(0, customers.Out);
        Assert.Equal(1, customers.In);
        Assert.Equal(1, customers.Degree);

        var edge = Assert.Single(graph.Edges);
        Assert.Equal("tables/orders", edge.Source);
        Assert.Equal("tables/customers", edge.Target);
        // Link text was "customers"
        Assert.Equal("customers", edge.Label);
        Assert.Equal("to_tc", edge.Id);
    }

    [Fact]
    public void Body_is_included_when_requested()
    {
        var graph = GraphBuilder.Build(FixturePath("valid"), includeBody: true);

        var orders = graph.Nodes.First(n => n.Id == "tables/orders");
        Assert.NotNull(orders.Body);
        Assert.Contains("customers", orders.Body);

        var graphNoBody = GraphBuilder.Build(FixturePath("valid"), includeBody: false);
        var orders2 = graphNoBody.Nodes.First(n => n.Id == "tables/orders");
        Assert.Null(orders2.Body);
    }

    [Fact]
    public void Nodes_without_type_are_skipped()
    {
        var bundlePath = Path.Combine(Path.GetTempPath(), "okf-graph-debug-skip");
        if (Directory.Exists(bundlePath)) Directory.Delete(bundlePath, recursive: true);
        Directory.CreateDirectory(bundlePath);

        try
        {
            // valid
            File.WriteAllText(Path.Combine(bundlePath, "good.md"), """
                ---
                type: Reference
                title: Good
                ---
                See [other](other.md).
                """);

            // missing type -> skipped (but must parse successfully)
            File.WriteAllText(Path.Combine(bundlePath, "bad.md"), """
                ---
                title: Bad No Type
                ---

                """);

            // other (no body text, but explicit newline after closing fm so parser succeeds)
            File.WriteAllText(Path.Combine(bundlePath, "other.md"), """
                ---
                type: Reference
                title: Other
                ---

                """);

            var graph = GraphBuilder.Build(bundlePath);

            // Debug aid - write to a side file that will be easy to inspect
            var dbg = Path.Combine(bundlePath, "debug-nodes.txt");
            File.WriteAllText(dbg, "NODES:" + string.Join(",", graph.Nodes.Select(n => n.Id + "(" + n.Type + ")")) + "\nFILES:" + string.Join(",", Directory.GetFiles(bundlePath, "*.md").Select(Path.GetFileName)));

            Assert.Equal(2, graph.Nodes.Count);
            Assert.Equal(1, graph.Edges.Count);
            Assert.Contains(graph.Nodes, n => n.Id == "good");
            Assert.Contains(graph.Nodes, n => n.Id == "other");
            Assert.DoesNotContain(graph.Nodes, n => n.Id == "bad");
            Assert.DoesNotContain(graph.Nodes, n => n.Id == "bad");
        }
        finally
        {
            if (Directory.Exists(bundlePath)) Directory.Delete(bundlePath, recursive: true);
        }
    }

    [Fact]
    public void Label_falls_back_to_title_or_id_when_link_text_missing()
    {
        var bundlePath = Path.Combine(Path.GetTempPath(), $"okf-graph-{Guid.NewGuid():N}");
        Directory.CreateDirectory(bundlePath);

        try
        {
            File.WriteAllText(Path.Combine(bundlePath, "source.md"), """
                ---
                type: Reference
                title: Source Title
                ---
                See [](target.md).
                """);

            File.WriteAllText(Path.Combine(bundlePath, "target.md"), """
                ---
                type: Reference
                title: The Real Target Title
                ---
                Target content.
                """);

            var graph = GraphBuilder.Build(bundlePath);

            Assert.Equal(1, graph.Edges.Count);
            var edge = graph.Edges[0];
            // text was empty -> fallback to title
            Assert.Equal("The Real Target Title", edge.Label);
            Assert.Equal("source", edge.Source);
            Assert.Equal("target", edge.Target);
        }
        finally
        {
            if (Directory.Exists(bundlePath)) Directory.Delete(bundlePath, recursive: true);
        }
    }

    [Fact]
    public void Type_is_lifted_to_node_level_not_duplicated_in_meta()
    {
        var graph = GraphBuilder.Build(FixturePath("valid"));

        foreach (var node in graph.Nodes)
        {
            Assert.False(string.IsNullOrWhiteSpace(node.Type));
            Assert.DoesNotContain(node.Meta, kvp => string.Equals(kvp.Key, "type", StringComparison.Ordinal));
        }

        var orders = graph.Nodes.First(n => n.Id == "tables/orders");
        Assert.Equal("BigQuery Table", orders.Type);
        Assert.Equal("Orders", orders.Meta["title"]);
    }

    [Fact]
    public void Label_is_lifted_to_node_level_not_duplicated_in_meta()
    {
        var bundlePath = Path.Combine(Path.GetTempPath(), $"okf-graph-{Guid.NewGuid():N}");
        Directory.CreateDirectory(bundlePath);

        try
        {
            File.WriteAllText(Path.Combine(bundlePath, "labeled.md"), """
                ---
                type: Reference
                label: Display Name
                ---
                Content.
                """);

            File.WriteAllText(Path.Combine(bundlePath, "unlabeled.md"), """
                ---
                type: Reference
                title: Title Only
                ---
                Content.
                """);

            var graph = GraphBuilder.Build(bundlePath);

            var labeled = graph.Nodes.First(n => n.Id == "labeled");
            Assert.Equal("Display Name", labeled.Label);
            Assert.DoesNotContain(labeled.Meta, kvp => string.Equals(kvp.Key, "label", StringComparison.Ordinal));

            var unlabeled = graph.Nodes.First(n => n.Id == "unlabeled");
            Assert.Equal("Unlabeled", unlabeled.Label);
            Assert.DoesNotContain(unlabeled.Meta, kvp => string.Equals(kvp.Key, "label", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(bundlePath)) Directory.Delete(bundlePath, recursive: true);
        }
    }

    [Fact]
    public void Title_stays_in_meta_and_is_not_copied_to_node_label()
    {
        var graph = GraphBuilder.Build(FixturePath("valid"));

        var orders = graph.Nodes.First(n => n.Id == "tables/orders");
        Assert.Equal("Orders", orders.Label);
        Assert.Equal("Orders", orders.Meta["title"]);

        var customers = graph.Nodes.First(n => n.Id == "tables/customers");
        Assert.Equal("customers", customers.Label);

        var bundlePath = Path.Combine(Path.GetTempPath(), $"okf-graph-{Guid.NewGuid():N}");
        Directory.CreateDirectory(bundlePath);

        try
        {
            File.WriteAllText(Path.Combine(bundlePath, "untitled.md"), """
                ---
                type: Reference
                ---
                No title here.
                """);

            var untitledGraph = GraphBuilder.Build(bundlePath);
            var untitled = Assert.Single(untitledGraph.Nodes);
            Assert.Equal("Untitled", untitled.Label);
            Assert.DoesNotContain(untitled.Meta, kvp => string.Equals(kvp.Key, "title", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(bundlePath)) Directory.Delete(bundlePath, recursive: true);
        }
    }

    [Fact]
    public void Label_uses_index_link_text_when_concept_has_no_label()
    {
        var bundlePath = Path.Combine(Path.GetTempPath(), $"okf-graph-{Guid.NewGuid():N}");
        Directory.CreateDirectory(bundlePath);
        Directory.CreateDirectory(Path.Combine(bundlePath, "tables"));

        try
        {
            File.WriteAllText(Path.Combine(bundlePath, "index.md"), """
                # Tables

                * [Customer Records](tables/customers.md) - Customer master data.
                """);

            File.WriteAllText(Path.Combine(bundlePath, "tables", "customers.md"), """
                ---
                type: BigQuery Table
                title: Customers
                ---
                Customer data.
                """);

            var graph = GraphBuilder.Build(bundlePath);
            var customers = Assert.Single(graph.Nodes);

            Assert.Equal("Customer Records", customers.Label);
            Assert.Equal("Customers", customers.Meta["title"]);
        }
        finally
        {
            if (Directory.Exists(bundlePath)) Directory.Delete(bundlePath, recursive: true);
        }
    }

    [Fact]
    public void Label_uses_shortest_incoming_link_text_when_index_missing()
    {
        var bundlePath = Path.Combine(Path.GetTempPath(), $"okf-graph-{Guid.NewGuid():N}");
        Directory.CreateDirectory(bundlePath);

        try
        {
            File.WriteAllText(Path.Combine(bundlePath, "source.md"), """
                ---
                type: Reference
                ---
                See [the target concept](target.md) and [target](target.md).
                """);

            File.WriteAllText(Path.Combine(bundlePath, "target.md"), """
                ---
                type: Reference
                title: Target Title
                ---
                Target body.
                """);

            var graph = GraphBuilder.Build(bundlePath);
            var target = graph.Nodes.First(n => n.Id == "target");

            Assert.Equal("target", target.Label);
        }
        finally
        {
            if (Directory.Exists(bundlePath)) Directory.Delete(bundlePath, recursive: true);
        }
    }

    [Fact]
    public void Label_disambiguates_titlecased_id_fallback_with_parents()
    {
        var bundlePath = Path.Combine(Path.GetTempPath(), $"okf-graph-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(bundlePath, "family"));
        Directory.CreateDirectory(Path.Combine(bundlePath, "company"));

        try
        {
            File.WriteAllText(Path.Combine(bundlePath, "family", "account.md"), """
                ---
                type: Reference
                ---
                Family account.
                """);

            File.WriteAllText(Path.Combine(bundlePath, "company", "account.md"), """
                ---
                type: Reference
                ---
                Company account.
                """);

            var graph = GraphBuilder.Build(bundlePath);

            Assert.Equal("Account (Family)", graph.Nodes.First(n => n.Id == "family/account").Label);
            Assert.Equal("Account (Company)", graph.Nodes.First(n => n.Id == "company/account").Label);
        }
        finally
        {
            if (Directory.Exists(bundlePath)) Directory.Delete(bundlePath, recursive: true);
        }
    }

    [Fact]
    public void Index_with_frontmatter_still_provides_labels()
    {
        var bundlePath = Path.Combine(Path.GetTempPath(), $"okf-graph-{Guid.NewGuid():N}");
        Directory.CreateDirectory(bundlePath);

        try
        {
            File.WriteAllText(Path.Combine(bundlePath, "index.md"), """
                ---
                okf_version: "0.1"
                ---

                # Concepts

                * [Playbook Entry](playbook.md) - A playbook.
                """);

            File.WriteAllText(Path.Combine(bundlePath, "playbook.md"), """
                ---
                type: Playbook
                ---
                Steps go here.
                """);

            var graph = GraphBuilder.Build(bundlePath);
            var playbook = Assert.Single(graph.Nodes);

            Assert.Equal("Playbook Entry", playbook.Label);
        }
        finally
        {
            if (Directory.Exists(bundlePath)) Directory.Delete(bundlePath, recursive: true);
        }
    }

    [Fact]
    public void Produces_valid_json_with_lowercase_keys()
    {
        var graph = GraphBuilder.Build(FixturePath("valid"));
        var json = System.Text.Json.JsonSerializer.Serialize(graph, new JsonSerializerOptions { WriteIndented = true });

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("bundle", out var b));
        Assert.True(b.TryGetProperty("root", out _));
        Assert.True(b.TryGetProperty("timestamp", out _));
        Assert.True(b.TryGetProperty("concepts", out _));

        var firstNode = root.GetProperty("nodes")[0];
        Assert.True(firstNode.TryGetProperty("id", out _));
        Assert.True(firstNode.TryGetProperty("path", out _));
        Assert.True(firstNode.TryGetProperty("meta", out _));
        Assert.True(firstNode.TryGetProperty("label", out _));
        Assert.True(firstNode.TryGetProperty("in", out _));
        Assert.True(firstNode.TryGetProperty("out", out _));

        var edge = root.GetProperty("edges")[0];
        Assert.True(edge.TryGetProperty("source", out _));
        Assert.True(edge.TryGetProperty("target", out _));
        Assert.True(edge.TryGetProperty("label", out _));
        Assert.True(edge.TryGetProperty("id", out _));
    }

    [Fact]
    public void Load_round_trips_generated_graph()
    {
        var bundlePath = FixturePath("valid");
        var graphPath = Path.Combine(Path.GetTempPath(), $"okf-graph-{Guid.NewGuid():N}.json");

        try
        {
            GraphBuilder.Generate(bundlePath, graphPath);
            var loaded = GraphBuilder.Load(graphPath);

            Assert.Equal(2, loaded.Nodes.Count);
            Assert.Equal(1, loaded.Edges.Count);
            Assert.False(string.IsNullOrWhiteSpace(loaded.Edges[0].Id));
        }
        finally
        {
            if (File.Exists(graphPath))
                File.Delete(graphPath);
        }
    }

    [Fact]
    public void Load_assigns_short_ids_when_edge_id_is_missing()
    {
        var graphPath = Path.Combine(Path.GetTempPath(), $"okf-graph-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(graphPath, """
                {
                  "bundle": {
                    "name": "test",
                    "root": "/tmp",
                    "timestamp": "2026-01-01T00:00:00Z",
                    "concepts": 2
                  },
                  "nodes": [
                    { "id": "a", "path": "a.md", "type": "Reference", "degree": 1, "in": 0, "out": 1, "meta": {} },
                    { "id": "b", "path": "b.md", "type": "Reference", "degree": 1, "in": 1, "out": 0, "meta": {} }
                  ],
                  "edges": [
                    { "source": "a", "target": "b", "label": "b" }
                  ]
                }
                """);

            var loaded = GraphBuilder.Load(graphPath);

            Assert.Equal("a_b", Assert.Single(loaded.Edges).Id);
        }
        finally
        {
            if (File.Exists(graphPath))
                File.Delete(graphPath);
        }
    }

    [Fact]
    public void Load_preserves_existing_edge_ids_when_some_are_missing()
    {
        var graphPath = Path.Combine(Path.GetTempPath(), $"okf-graph-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(graphPath, """
                {
                  "bundle": {
                    "name": "test",
                    "root": "/tmp",
                    "timestamp": "2026-01-01T00:00:00Z",
                    "concepts": 3
                  },
                  "nodes": [
                    { "id": "a", "path": "a.md", "type": "Reference", "degree": 2, "in": 0, "out": 2, "meta": {} },
                    { "id": "b", "path": "b.md", "type": "Reference", "degree": 2, "in": 1, "out": 1, "meta": {} },
                    { "id": "c", "path": "c.md", "type": "Reference", "degree": 1, "in": 1, "out": 0, "meta": {} }
                  ],
                  "edges": [
                    { "source": "a", "target": "b", "label": "b", "id": "custom_ab" },
                    { "source": "b", "target": "c", "label": "c" }
                  ]
                }
                """);

            var loaded = GraphBuilder.Load(graphPath);

            Assert.Equal(2, loaded.Edges.Count);
            Assert.Equal("custom_ab", loaded.Edges[0].Id);
            Assert.Equal("b_c", loaded.Edges[1].Id);
        }
        finally
        {
            if (File.Exists(graphPath))
                File.Delete(graphPath);
        }
    }

    [Fact]
    public void Load_round_trips_unknown_properties_via_extension_data()
    {
        var graphPath = Path.Combine(Path.GetTempPath(), $"okf-graph-{Guid.NewGuid():N}.json");
        var roundTripPath = Path.Combine(Path.GetTempPath(), $"okf-graph-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(graphPath, """
                {
                  "bundle": {
                    "name": "test",
                    "root": "/tmp",
                    "timestamp": "2026-01-01T00:00:00Z",
                    "concepts": 1,
                    "producer": "acme-agent"
                  },
                  "nodes": [
                    {
                      "id": "a",
                      "path": "a.md",
                      "type": "Reference",
                      "degree": 0,
                      "in": 0,
                      "out": 0,
                      "meta": {},
                      "highlight": true
                    }
                  ],
                  "edges": [],
                  "schema_version": 2
                }
                """);

            var loaded = GraphBuilder.Load(graphPath);
            Assert.Equal("acme-agent", loaded.Bundle.ExtensionData!["producer"].GetString());
            Assert.Equal(2, loaded.ExtensionData!["schema_version"].GetInt32());
            Assert.True(loaded.Nodes[0].ExtensionData!["highlight"].GetBoolean());

            var json = JsonSerializer.Serialize(loaded, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(roundTripPath, json);
            var reloaded = GraphBuilder.Load(roundTripPath);

            Assert.Equal("acme-agent", reloaded.Bundle.ExtensionData!["producer"].GetString());
            Assert.Equal(2, reloaded.ExtensionData!["schema_version"].GetInt32());
            Assert.True(reloaded.Nodes[0].ExtensionData!["highlight"].GetBoolean());

            using var document = JsonDocument.Parse(json);
            Assert.Equal("acme-agent", document.RootElement.GetProperty("bundle").GetProperty("producer").GetString());
            Assert.Equal(2, document.RootElement.GetProperty("schema_version").GetInt32());
            Assert.True(document.RootElement.GetProperty("nodes")[0].GetProperty("highlight").GetBoolean());
        }
        finally
        {
            if (File.Exists(graphPath))
                File.Delete(graphPath);
            if (File.Exists(roundTripPath))
                File.Delete(roundTripPath);
        }
    }
}
