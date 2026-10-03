namespace Aibysitter.Web.GitHub;

/// <param name="Paths">Every blob path at the head commit, symlinks included.</param>
/// <param name="Symlinks">Paths whose tree mode is 120000.</param>
public sealed record RepoTree(IReadOnlyList<string> Paths, IReadOnlySet<string> Symlinks);
