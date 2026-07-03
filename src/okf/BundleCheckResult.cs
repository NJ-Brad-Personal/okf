namespace Devlooped;

public sealed record BundleCheckResult(
    IReadOnlyList<ValidationIssue> Errors,
    IReadOnlyList<ValidationIssue> Warnings);