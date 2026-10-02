namespace Aibysitter.Rules.Rules;

/// <summary>A Cursor rule in Apply Manually mode: alwaysApply not true, no globs, no description. Cursor includes it only when @-mentioned.</summary>
public sealed class ManualCursorRule : IRule
{
    public string Id => "R016";
    public string Title => "Manual Cursor rule";
    public Severity Severity => Severity.Info;

    public IEnumerable<Finding> Evaluate(RulesFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.Format == RulesFormat.CursorMdc && file.Frontmatter is { } fm && IsManual(fm))
        {
            yield return new Finding(
                Id,
                1,
                "Manual rule: Cursor includes it only when @-mentioned.",
                "To apply it automatically, set alwaysApply: true, add globs, or add a description.");
        }
    }

    private static bool IsManual(Frontmatter fm) =>
        fm.Values("alwaysApply") is not ["true"] && fm.Values("globs").Count == 0 && fm.Values("description").Count == 0;
}
