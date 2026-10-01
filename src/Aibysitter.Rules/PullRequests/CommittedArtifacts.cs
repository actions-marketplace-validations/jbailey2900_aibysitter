using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

/// <summary>Build output, dependency folders, caches, and .env files added by the pull request.</summary>
public sealed partial class CommittedArtifacts : IPullRequestCheck
{
    public string Id => "P013";
    public string Title => "Committed artifacts";
    public Severity Severity => Severity.Error;

    public IEnumerable<PullRequestFinding> Evaluate(PullRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var file in context.Files.Where(f => f.Status is FileChangeStatus.Added or FileChangeStatus.Renamed or FileChangeStatus.Copied))
        {
            var kind = Classify(file.Path);
            if (kind is not null)
            {
                yield return new PullRequestFinding(
                    Id,
                    file.Path,
                    1,
                    $"{kind} committed: {file.Path}",
                    "Remove the file from the PR and add its pattern to .gitignore.");
            }
        }
    }

    public static string? Classify(string path)
    {
        var p = path.Replace('\\', '/');
        var name = p[(p.LastIndexOf('/') + 1)..];

        if (NodeModulesRegex().IsMatch(p))
        {
            return "Dependency folder (node_modules)";
        }

        if (DotNetOutputRegex().IsMatch(p))
        {
            return ".NET build output";
        }

        if (PyCacheRegex().IsMatch(p))
        {
            return "Python bytecode cache";
        }

        if (EnvFileRegex().IsMatch(name))
        {
            return "Environment file";
        }

        return null;
    }

    [GeneratedRegex(@"(?:^|/)node_modules/")]
    private static partial Regex NodeModulesRegex();

    /// <summary>bin/ or obj/ under Debug or Release, or obj/ restore files. A bare bin/ folder of scripts is not matched.</summary>
    [GeneratedRegex(@"(?:^|/)(?:bin|obj)/(?:Debug|Release)/|(?:^|/)obj/(?:project\.assets\.json|[^/]+\.nuget\.(?:g\.props|g\.targets|dgspec\.json)|project\.nuget\.cache)$", RegexOptions.IgnoreCase)]
    private static partial Regex DotNetOutputRegex();

    [GeneratedRegex(@"(?:^|/)__pycache__/|\.py[co]$")]
    private static partial Regex PyCacheRegex();

    /// <summary>.env and .env.* except .env.example / .env.sample / .env.template / .env.dist.</summary>
    [GeneratedRegex(@"^\.env(?:\.(?!(?:example|sample|template|dist)$)[\w.-]+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex EnvFileRegex();
}
