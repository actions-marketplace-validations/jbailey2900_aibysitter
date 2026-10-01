namespace Aibysitter.Rules.PullRequests;

public enum FileChangeStatus
{
    Added,
    Modified,
    Removed,
    Renamed,
    Copied,
    Changed,
    Unchanged,
}

/// <summary>
/// A file changed by a pull request. Patch is GitHub's per-file hunk text (null for binary or oversized files).
/// HeadContent is the full file at the head commit, when fetched.
/// </summary>
public sealed record ChangedFile(
    string Path,
    FileChangeStatus Status,
    string? Patch = null,
    string? PreviousPath = null,
    string? HeadContent = null)
{
    private IReadOnlyList<DiffLine>? lines;

    public IReadOnlyList<DiffLine> Lines => lines ??= PatchParser.Parse(Patch);

    public IEnumerable<DiffLine> AddedLines => Lines.Where(l => l.Kind == DiffLineKind.Added);
}
