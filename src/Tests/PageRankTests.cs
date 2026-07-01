using okf;
using Xunit;

namespace Tests;

public class PageRankTests
{
    static GraphBuilder.Node Node(string id)
        => new(id, "Reference")
        {
            Path = $"{id}.md",
            Degree = 0,
            In = 0,
            Out = 0,
            Meta = [],
        };

    [Fact]
    public void Single_node_has_unit_weight()
    {
        var weights = PageRank.Compute([Node("only")], []);

        Assert.Equal(1.0, Assert.Single(weights).Value, precision: 6);
    }

    [Fact]
    public void Linear_chain_gives_highest_weight_to_sink()
    {
        var nodes = new[] { Node("a"), Node("b"), Node("c") };
        var edges = new[]
        {
            new GraphBuilder.Edge("a", "b", "a_b"),
            new GraphBuilder.Edge("b", "c", "b_c"),
        };

        var weights = PageRank.Compute(nodes, edges);

        Assert.True(weights["c"] > weights["b"]);
        Assert.True(weights["b"] > weights["a"]);
        Assert.Equal(1.0, weights.Values.Sum(), precision: 6);
    }

    [Fact]
    public void Disconnected_nodes_share_weight_evenly()
    {
        var nodes = new[] { Node("a"), Node("b"), Node("c") };
        var weights = PageRank.Compute(nodes, []);

        Assert.Equal(1.0 / 3, weights["a"], precision: 6);
        Assert.Equal(1.0 / 3, weights["b"], precision: 6);
        Assert.Equal(1.0 / 3, weights["c"], precision: 6);
    }
}