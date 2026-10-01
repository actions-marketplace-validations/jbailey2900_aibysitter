namespace Aibysitter.Rules.PullRequests;

public sealed record PullRequestContext(IReadOnlyList<ChangedFile> Files, RepoConfig Config);
