namespace Devlooped;

public sealed record IssueLocation(
    int Line,
    int? Column = null,
    int? EndLine = null,
    int? EndColumn = null)
{
    public string FormatSuffix()
    {
        if (Column is not int column)
        {
            return $"({Line})";
        }

        if (EndLine is int endLine && EndColumn is int endColumn
            && (endLine != Line || endColumn != column))
        {
            return $"({Line}:{column}-{endLine}:{endColumn})";
        }

        return $"({Line}:{column})";
    }

    public static IssueLocation? FromYamlSnippet(string text, SourceSnippet? snippet)
    {
        if (snippet?.Lines is not { Count: > 0 } lines)
        {
            return null;
        }

        if (!text.StartsWith("---", StringComparison.Ordinal))
        {
            return null;
        }

        const int frontmatterLineOffset = 1;
        var first = lines[0];
        var last = lines[^1];
        var startLine = first.LineNumber + frontmatterLineOffset;
        var endLine = last.LineNumber + frontmatterLineOffset;

        if (startLine == endLine && first.StartColumn == last.EndColumn)
        {
            return new IssueLocation(startLine, first.StartColumn);
        }

        if (startLine == endLine)
        {
            return new IssueLocation(startLine, first.StartColumn, endLine, last.EndColumn);
        }

        return new IssueLocation(startLine, first.StartColumn, endLine, last.EndColumn);
    }
}
