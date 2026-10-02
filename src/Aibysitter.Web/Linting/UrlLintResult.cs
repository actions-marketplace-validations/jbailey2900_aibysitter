namespace Aibysitter.Web.Linting;

/// <summary>What a lint-by-URL request found. <see cref="Error"/> is a plain message when nothing was linted.</summary>
public sealed record UrlLintResult(RepoRef? Repo, string? FileName, IReadOnlyList<string> OtherFiles, string? Error)
{
    public const string InvalidInput = "Enter a GitHub repository as owner/repo or its URL.";

    public Uri? RawUrl => Repo is not null && FileName is not null ? RawGitHubFetcher.RawUrl(Repo, FileName) : null;

    public static UrlLintResult Invalid() => new(null, null, [], InvalidInput);

    public static UrlLintResult From(RepoRef repo, FetchResult fetched) => new(repo, fetched.FileName, fetched.OtherFiles, fetched.Status switch
    {
        FetchStatus.Found => null,
        FetchStatus.TooLarge => $"{fetched.FileName} is over {RawGitHubFetcher.MaxBytes / 1024} KB.",
        FetchStatus.TimedOut => "GitHub did not respond within 5 seconds.",
        FetchStatus.Unreachable => "GitHub could not be reached. Try again later.",
        _ => $"No supported rules file found on the default branch of {repo}. Private repositories can't be read.",
    });
}
