using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

/// <summary>Base for checks that flag added lines matching a pattern. One finding per line.</summary>
public abstract class AddedLinePatternCheck : IPullRequestCheck
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract Severity Severity { get; }

    protected abstract Regex Pattern { get; }
    protected abstract string MessagePrefix { get; }
    protected abstract string FixHint { get; }

    protected abstract bool AppliesTo(string path);

    public IEnumerable<PullRequestFinding> Evaluate(PullRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var file in context.Files.Where(f => f.Status != FileChangeStatus.Removed && AppliesTo(f.Path)))
        {
            foreach (var line in file.AddedLines)
            {
                var matches = Pattern.Matches(line.Text).Select(m => m.Value.Trim()).Distinct().ToList();
                if (matches.Count > 0)
                {
                    yield return new PullRequestFinding(
                        Id,
                        file.Path,
                        line.NewLine!.Value,
                        $"{MessagePrefix}: {string.Join(", ", matches.Select(m => $"\"{m}\""))}",
                        FixHint);
                }
            }
        }
    }
}
