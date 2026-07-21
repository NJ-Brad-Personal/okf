using System.Text;

namespace Devlooped;

/// <summary>
/// Generates missing <c>index.md</c> files for a bundle per SPEC §6, reusing the
/// same listing logic <see cref="IndexNavBuilder"/> otherwise synthesizes in-memory
/// for navigation (<c>view</c>/<c>graph --nav</c>) when a directory has no authored index.
/// Directories that already have an <c>index.md</c> are left untouched.
/// </summary>
public static class IndexGenerator
{
    // UTF-8 without BOM, consistent with other generated files (okf.json/okf.js).
    static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public sealed record GeneratedIndex(string Path, string Body, bool Existed);

    /// <summary>
    /// Writes an <c>index.md</c> file for every directory in the bundle that doesn't
    /// already have one. Returns the set of files created (or that would be created,
    /// when <paramref name="dryRun"/> is true), relative to the bundle root.
    /// </summary>
    /// <param name="force">
    /// When true, regenerate every directory's index.md from current concept
    /// frontmatter, overwriting any authored content (a full refresh that picks up
    /// renamed/added/removed concept files). When false (default), directories that
    /// already have an index.md are left untouched.
    /// </param>
    public static IReadOnlyList<GeneratedIndex> Generate(string bundleRoot, bool force = false, bool dryRun = false)
    {
        bundleRoot = Path.GetFullPath(bundleRoot);

        var graph = GraphBuilder.Build(bundleRoot);
        var nav = IndexNavBuilder.Build(bundleRoot, graph.Nodes, forceSynthesize: force);
        var results = new List<GeneratedIndex>();

        Walk(nav, bundleRoot, results, dryRun);

        return results;
    }

    static void Walk(GraphBuilder.NavNode node, string bundleRoot, List<GeneratedIndex> results, bool dryRun)
    {
        if (node.Kind == "dir" && node.Synthetic == true)
        {
            // node.Id is the %20-encoded concept id (SPEC filename-with-spaces convention);
            // decode back to a real space before touching the filesystem.
            var indexRel = string.IsNullOrEmpty(node.Id) ? "index.md" : node.Id + "/index.md";
            indexRel = indexRel.Replace("%20", " ");
            var indexAbs = Path.Combine(bundleRoot, indexRel.Replace('/', Path.DirectorySeparatorChar));
            var existed = File.Exists(indexAbs);
            var body = node.Body ?? "";

            if (!dryRun)
            {
                File.WriteAllText(indexAbs, body, Utf8NoBom);
            }

            results.Add(new GeneratedIndex(indexRel, body, existed));
        }

        foreach (var child in node.Children ?? [])
        {
            Walk(child, bundleRoot, results, dryRun);
        }
    }
}
