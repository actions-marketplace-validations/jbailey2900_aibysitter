using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

/// <summary>CI configuration changed: GitHub Actions workflows and actions, GitLab, Azure Pipelines, CircleCI, Jenkins.</summary>
public sealed partial class CiConfigEdited : IPullRequestCheck
{
    public string Id => "P011";
    public string Title => "CI config edited";
    public Severity Severity => Severity.Info;

    public IEnumerable<PullRequestFinding> Evaluate(PullRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var file in context.Files.Where(f => IsCiConfig(f.Path) || (f.PreviousPath is not null && IsCiConfig(f.PreviousPath))))
        {
            yield return new PullRequestFinding(
                Id,
                file.Path,
                1,
                $"CI config {file.Status.ToString().ToLowerInvariant()}: {file.Path}",
                "Confirm the CI change was requested. A change that weakens CI to pass the build needs review.");
        }
    }

    public static bool IsCiConfig(string path) => CiPathRegex().IsMatch(path.Replace('\\', '/'));

    [GeneratedRegex(@"^\.github/(?:workflows|actions)/|^\.gitlab-ci\.ya?ml$|^\.gitlab/ci/|^azure-pipelines[\w.-]*\.ya?ml$|^\.azure-pipelines/|^\.circleci/|(?:^|/)Jenkinsfile$|^\.buildkite/|^bitbucket-pipelines\.yml$", RegexOptions.IgnoreCase)]
    private static partial Regex CiPathRegex();
}
