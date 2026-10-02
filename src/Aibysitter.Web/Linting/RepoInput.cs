using System.Text.RegularExpressions;

namespace Aibysitter.Web.Linting;

/// <param name="HasExtraPath">The URL named a branch, folder, or file; only the default branch is linted.</param>
public sealed record RepoRef(string Owner, string Repo, bool HasExtraPath)
{
    public override string ToString() => $"{Owner}/{Repo}";
}

/// <summary>
/// Parses <c>owner/repo</c>, <c>github.com/owner/repo</c>, or an http(s) github.com URL. Owner and repo must be valid
/// GitHub names; nothing else from the input reaches a fetch URL.
/// </summary>
public static partial class RepoInput
{
    public static bool TryParse(string? input, out RepoRef? repo)
    {
        repo = null;
        var text = input?.Trim() ?? string.Empty;
        if (text.Length is 0 or > 300)
        {
            return false;
        }

        if (ShortFormRegex().Match(text) is { Success: true } shortForm)
        {
            return TryBuild(shortForm.Groups["owner"].Value, shortForm.Groups["repo"].Value, extraPath: false, out repo);
        }

        var url = text.StartsWith("github.com/", StringComparison.OrdinalIgnoreCase) || text.StartsWith("www.github.com/", StringComparison.OrdinalIgnoreCase)
            ? "https://" + text
            : text;
        return TryParseUrl(url, out repo);
    }

    private static bool TryParseUrl(string url, out RepoRef? repo)
    {
        repo = null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http")
            || uri.Host is not ("github.com" or "www.github.com")
            || !uri.IsDefaultPort
            || uri.UserInfo.Length > 0)
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 && TryBuild(segments[0], segments[1], segments.Length > 2, out repo);
    }

    private static bool TryBuild(string owner, string repo, bool extraPath, out RepoRef? result)
    {
        result = null;
        if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            repo = repo[..^4];
        }

        if (!OwnerRegex().IsMatch(owner) || !RepoRegex().IsMatch(repo) || repo is "." or "..")
        {
            return false;
        }

        result = new RepoRef(owner, repo, extraPath);
        return true;
    }

    [GeneratedRegex(@"^(?<owner>[^/\s]+)/(?<repo>[^/\s]+)$")]
    private static partial Regex ShortFormRegex();

    [GeneratedRegex(@"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$")]
    private static partial Regex OwnerRegex();

    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,100}$")]
    private static partial Regex RepoRegex();
}
