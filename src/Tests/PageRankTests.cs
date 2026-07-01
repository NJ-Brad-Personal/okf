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
        };

    [Fact]
    public void Single_node_has_unit_weight_and_rank_one()
    {
        var result = PageRank.Compute([Node("only")], []);

        Assert.Equal(1.0, Assert.Single(result.Weights).Value, precision: 6);
        Assert.Equal(1, result.Ranks["only"]);
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

        var result = PageRank.Compute(nodes, edges);

        Assert.True(result.Weights["c"] > result.Weights["b"]);
        Assert.True(result.Weights["b"] > result.Weights["a"]);
        Assert.Equal(1.0, result.Weights.Values.Sum(), precision: 6);
        Assert.Equal(1, result.Ranks["c"]);
        Assert.Equal(2, result.Ranks["b"]);
        Assert.Equal(3, result.Ranks["a"]);
    }

    [Fact]
    public void Disconnected_nodes_share_weight_evenly_and_rank_one()
    {
        var nodes = new[] { Node("a"), Node("b"), Node("c") };
        var result = PageRank.Compute(nodes, []);

        Assert.Equal(1.0 / 3, result.Weights["a"], precision: 6);
        Assert.Equal(1.0 / 3, result.Weights["b"], precision: 6);
        Assert.Equal(1.0 / 3, result.Weights["c"], precision: 6);
        Assert.All(result.Ranks.Values, rank => Assert.Equal(1, rank));
    }

    [Fact]
    public void ComputeRanks_uses_competition_ranking_for_ties()
    {
        var weights = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["a"] = 0.5,
            ["b"] = 0.3,
            ["c"] = 0.3,
            ["d"] = 0.1,
        };

        var ranks = PageRank.ComputeRanks(weights);

        Assert.Equal(1, ranks["a"]);
        Assert.Equal(2, ranks["b"]);
        Assert.Equal(2, ranks["c"]);
        Assert.Equal(4, ranks["d"]);
    }
}