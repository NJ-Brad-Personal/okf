namespace okf;

public enum IssueSeverity
{
    Error,
    Warning,
}

public sealed record ValidationIssue(
    CheckRule Rule,
    IssueSeverity Severity,
    string File,
    string Message,
    IssueLocation? Location = null,
    SourceSnippet? Snippet = null)
{
    public override string ToString()
        => Location is null
            ? $"{Severity.ToString().ToLowerInvariant()}: {File}: {Message}"
            : $"{Severity.ToString().ToLowerInvariant()}: {File}{Location.FormatSuffix()}: {Message}";
}
