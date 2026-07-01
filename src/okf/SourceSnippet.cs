namespace okf;

public sealed record HighlightedSourceLine(
    int LineNumber,
    string Text,
    int StartColumn,
    int EndColumn);

public sealed record SourceSnippet(IReadOnlyList<HighlightedSourceLine> Lines);
