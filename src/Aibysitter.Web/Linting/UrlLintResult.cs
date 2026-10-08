namespace Aibysitter.Web.Linting;

/// <summary>What a lint-by-URL request found. <see cref="Error"/> is a plain message when nothing was linted.</summary>
/// <param name="LinkTarget">Set when <see cref="FileName"/> is a symlink; the target's content was linted.</param>
public sealed record UrlLintResult(RepoRef? Repo, string? FileName, IReadOnlyList<string> OtherFiles, string? Error, string? LinkTarget = null)
{
    public const string InvalidInput = "Enter a GitHub repository as owner/repo or its URL.";

    /// <summary>The file whose content was linted: the link target when there is one.</summary>
    public string? LintedPath => LinkTarget ?? FileName;

    public Uri? RawUrl => Repo is not null && LintedPath is not null ? RawGitHubFetcher.RawUrl(Repo, LintedPath) : null;

    public static UrlLintResult Invalid() => new(null, null, [], InvalidInput);

    public static UrlLintResult From(RepoRef repo, FetchResult fetched) => new(repo, fetched.FileName, fetched.OtherFiles, fetched.Status switch
    {
        FetchStatus.Found => null,
        FetchStatus.TooLarge => $"{fetched.LinkTarget ?? fetched.FileName} is over {RawGitHubFetcher.MaxBytes / 1024} KB.",
        FetchStatus.LinkTargetMissing => $"{fetched.FileName} links to {fetched.LinkTarget}, which was not found on the default branch.",
        FetchStatus.LinkTooDeep => $"{fetched.FileName} links to {fetched.LinkTarget}, which looks like a link to {fetched.NextLink}. Links are followed one level.",
        FetchStatus.NotUtf8 => $"{fetched.LinkTarget ?? fetched.FileName} is not UTF-8 text; it was not linted.",
        FetchStatus.TimedOut => "GitHub did not respond within 5 seconds.",
        FetchStatus.Unreachable => "GitHub could not be reached. Try again later.",
        _ => $"No supported rules file found on the default branch of {repo}. Private repositories can't be read. Cursor .mdc rules: paste them above.",
    }, fetched.LinkTarget);
}
