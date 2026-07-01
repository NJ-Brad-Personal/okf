using Spectre.Console;

namespace okf;

public static class CheckRenderer
{
    public static void Render(IReadOnlyList<ValidationIssue> issues, string bundleRoot, bool linksChecked = true)
    {
        var bundleRootFull = Path.GetFullPath(bundleRoot);
        var issuesByRule = issues
            .GroupBy(issue => issue.Rule)
            .ToDictionary(group => group.Key, group => group.ToList());

        var rules = linksChecked
            ? CheckRules.All
            : CheckRules.All.Where(rule => rule.Rule != CheckRule.InternalLinks).ToArray();

        if (issuesByRule.ContainsKey(CheckRule.BundleExists))
        {
            rules = [(CheckRule.BundleExists, GetDescription(CheckRule.BundleExists))];
        }

        foreach (var (rule, description) in rules)
        {
            if (issuesByRule.TryGetValue(rule, out var ruleIssues))
            {
                AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(description)}");
                foreach (var issue in ruleIssues)
                {
                    AnsiConsole.MarkupLine($"  {FormatIssue(issue, bundleRootFull)}");
                    if (issue.Snippet is not null)
                    {
                        foreach (var line in issue.Snippet.Lines)
                        {
                            AnsiConsole.MarkupLine($"    {FormatSnippetLine(line)}");
                        }
                    }
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(description)}");
            }
        }
    }

    static string GetDescription(CheckRule rule)
        => CheckRules.All.First(entry => entry.Rule == rule).Description;

    static string FormatSnippetLine(HighlightedSourceLine line)
        => $"[dim]{Markup.Escape(line.Text)}[/]";

    static string FormatIssue(ValidationIssue issue, string bundleRootFull)
    {
        var (fullPath, displayPath) = ResolvePaths(issue.File, bundleRootFull);
        var locationSuffix = issue.Location?.FormatSuffix() ?? string.Empty;
        var link = $"[link={fullPath}]{Markup.Escape(displayPath + locationSuffix)}[/]";
        return $"{link}: {Markup.Escape(issue.Message)}";
    }

    static (string FullPath, string DisplayPath) ResolvePaths(string file, string bundleRootFull)
    {
        if (Path.IsPathRooted(file))
        {
            var fullPath = Path.GetFullPath(file);
            return (fullPath, fullPath);
        }

        var relativePath = file.Replace('\\', '/');
        var full = Path.GetFullPath(Path.Combine(bundleRootFull, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return (full, relativePath);
    }
}
