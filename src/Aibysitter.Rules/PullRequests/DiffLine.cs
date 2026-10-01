namespace Aibysitter.Rules.PullRequests;

public enum DiffLineKind
{
    Context,
    Added,
    Removed,
}

/// <summary>One line of a unified diff hunk. OldLine is null for added lines; NewLine is null for removed lines.</summary>
public sealed record DiffLine(DiffLineKind Kind, int? OldLine, int? NewLine, string Text);
