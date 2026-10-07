using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

/// <summary>
/// Added <c>uses:</c> lines in GitHub workflow and action files whose reference is not a 40-character commit SHA.
/// Local actions (<c>./</c>) and <c>docker://</c> images are not checked.
/// </summary>
public sealed partial class UnpinnedActions : IPullRequestCheck
{
    public string Id => "P015";
    public string Title => "Unpinned actions";
    public Severity Severity => Severity.Warning;

    public IEnumerable<PullRequestFinding> Evaluate(PullRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var file in context.Files.Where(f => f.Status != FileChangeStatus.Removed && IsActionsFile(f.Path)))
        {
            foreach (var line in file.AddedLines.Where(l => !CodeText.IsCommentOnly(l.Text)))
            {
                var reference = UsesRegex().Match(line.Text) is { Success: true } m ? m.Groups["ref"].Value : null;
                if (reference is null || reference.StartsWith("./", StringComparison.Ordinal) || reference.StartsWith("docker://", StringComparison.Ordinal))
                {
                    continue;
                }

                var at = reference.LastIndexOf('@');
                if (at < 0 || !ShaRegex().IsMatch(reference[(at + 1)..]))
                {
                    yield return new PullRequestFinding(
                        Id,
                        file.Path,
                        line.NewLine!.Value,
                        $"Action not pinned to a commit SHA: {reference}",
                        "Pin to the full commit SHA and keep the tag in a comment, for example actions/checkout@<sha> # v5.");
                }
            }
        }
    }

    public static bool IsActionsFile(string path) => ActionsPathRegex().IsMatch(path.Replace('\\', '/'));

    [GeneratedRegex(@"^\.github/(?:workflows|actions)/.+\.ya?ml$|(?:^|/)action\.ya?ml$", RegexOptions.IgnoreCase)]
    private static partial Regex ActionsPathRegex();

    [GeneratedRegex(@"^\s*(?:-\s*)?uses:\s*[""']?(?<ref>[^\s""'#]+)")]
    private static partial Regex UsesRegex();

    [GeneratedRegex(@"^[0-9a-f]{40}$")]
    private static partial Regex ShaRegex();
}
