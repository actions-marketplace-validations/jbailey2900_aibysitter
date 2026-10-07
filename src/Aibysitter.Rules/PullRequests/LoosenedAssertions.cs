using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

/// <summary>
/// Modified test files where one hunk removes an exact assertion (Assert.Equal, StrictEqual, Same, Single, Empty, AreEqual,
/// AreSame; toBe, toEqual, toStrictEqual) and adds a weaker one (Assert.Contains, DoesNotContain, NotNull, True, Matches,
/// InRange, NotEmpty, NotEqual, IsTrue, IsNotNull; toContain, toBeTruthy, toMatch, toBeDefined) without adding back as many
/// exact assertions. One finding per hunk, on the first weaker assertion.
/// </summary>
public sealed partial class LoosenedAssertions : IPullRequestCheck
{
    public string Id => "P017";
    public string Title => "Loosened assertions";
    public Severity Severity => Severity.Warning;

    public IEnumerable<PullRequestFinding> Evaluate(PullRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var file in context.Files.Where(f => f.Status == FileChangeStatus.Modified && FileKinds.IsTestFile(f.Path) && f.Patch is not null))
        {
            foreach (var hunk in HunkRegex().Split(file.Patch!).Where(h => h.StartsWith("@@", StringComparison.Ordinal)))
            {
                var lines = PatchParser.Parse(hunk);
                var removedExact = lines.Where(l => l.Kind == DiffLineKind.Removed).Select(l => ExactRegex().Match(l.Text)).Where(m => m.Success).ToList();
                var addedExact = lines.Count(l => l.Kind == DiffLineKind.Added && ExactRegex().IsMatch(l.Text));
                var weaker = lines.FirstOrDefault(l => l.Kind == DiffLineKind.Added && WeakerRegex().IsMatch(l.Text) && !ExactRegex().IsMatch(l.Text));

                if (removedExact.Count > 0 && weaker is not null && addedExact < removedExact.Count)
                {
                    yield return new PullRequestFinding(
                        Id,
                        file.Path,
                        weaker.NewLine!.Value,
                        $"Assertion loosened: {removedExact[0].Groups["a"].Value} replaced by {WeakerRegex().Match(weaker.Text).Groups["a"].Value}",
                        "Keep the exact assertion and fix the code, or state in the PR why the expected value changed.");
                }
            }
        }
    }

    [GeneratedRegex(@"\n(?=@@ )")]
    private static partial Regex HunkRegex();

    [GeneratedRegex(@"\b(?<a>Assert\.(?:Equal|StrictEqual|Same|Single|Empty|AreEqual|AreSame))\s*[(<]|\.(?<a>toBe|toEqual|toStrictEqual)\s*\(")]
    private static partial Regex ExactRegex();

    [GeneratedRegex(@"\b(?<a>Assert\.(?:Contains|DoesNotContain|NotNull|True|Matches|InRange|NotEmpty|NotEqual|IsTrue|IsNotNull))\s*[(<]|\.(?<a>toContain|toBeTruthy|toMatch|toBeDefined)\s*\(")]
    private static partial Regex WeakerRegex();
}
