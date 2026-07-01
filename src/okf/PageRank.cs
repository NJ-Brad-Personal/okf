namespace okf;

public static class PageRank
{
    const double Damping = 0.85;
    const int MaxIterations = 100;
    const double Tolerance = 1e-6;

    public static IReadOnlyDictionary<string, double> Compute(
        IReadOnlyList<GraphBuilder.Node> nodes,
        IReadOnlyList<GraphBuilder.Edge> edges)
    {
        var nodeIds = nodes.Select(n => n.Id).ToList();
        var count = nodeIds.Count;

        if (count == 0)
            return new Dictionary<string, double>(StringComparer.Ordinal);

        if (count == 1)
            return new Dictionary<string, double>(StringComparer.Ordinal) { [nodeIds[0]] = 1.0 };

        var index = nodeIds
            .Select((id, i) => (id, i))
            .ToDictionary(x => x.id, x => x.i, StringComparer.Ordinal);

        var incoming = new List<int>[count];
        for (var i = 0; i < count; i++)
            incoming[i] = [];

        var outDegree = new int[count];
        foreach (var edge in edges)
        {
            if (!index.TryGetValue(edge.Source, out var sourceIndex)
                || !index.TryGetValue(edge.Target, out var targetIndex))
            {
                continue;
            }

            incoming[targetIndex].Add(sourceIndex);
            outDegree[sourceIndex]++;
        }

        var ranks = new double[count];
        var nextRanks = new double[count];
        Array.Fill(ranks, 1.0 / count);

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            var danglingSum = 0.0;
            for (var i = 0; i < count; i++)
            {
                if (outDegree[i] == 0)
                    danglingSum += ranks[i];
            }

            var baseRank = (1.0 - Damping) / count;
            var danglingContribution = Damping * danglingSum / count;
            var delta = 0.0;

            for (var i = 0; i < count; i++)
            {
                var inbound = 0.0;
                foreach (var sourceIndex in incoming[i])
                    inbound += ranks[sourceIndex] / outDegree[sourceIndex];

                nextRanks[i] = baseRank + danglingContribution + Damping * inbound;
                delta += Math.Abs(nextRanks[i] - ranks[i]);
            }

            (ranks, nextRanks) = (nextRanks, ranks);

            if (delta < Tolerance)
                break;
        }

        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
            result[nodeIds[i]] = ranks[i];

        return result;
    }
}