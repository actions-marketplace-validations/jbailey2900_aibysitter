namespace Aibysitter.Rules.Rules;

/// <summary>
/// Cursor .mdc rules only: frontmatter present, known keys, boolean alwaysApply, and at least one way for the rule
/// to apply (alwaysApply: true, globs, or a description).
/// </summary>
public sealed class FrontmatterFields : IRule
{
    public string Id => "R015";
    public string Title => "Frontmatter fields";
    public Severity Severity => Severity.Warning;

    public IEnumerable<Finding> Evaluate(RulesFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.Format != RulesFormat.CursorMdc)
        {
            yield break;
        }

        if (file.Frontmatter is not { } fm)
        {
            yield return new Finding(Id, 1, "Cursor rule has no frontmatter.", "Start the file with a --- block containing description, globs, and alwaysApply.");
            yield break;
        }

        foreach (var key in fm.Keys.Where(k => !RulesFormats.CursorKeys.Contains(k)).Order(StringComparer.Ordinal))
        {
            yield return new Finding(Id, fm.LineOf(key), $"Unknown frontmatter key \"{key}\"; Cursor reads description, globs, alwaysApply.", "Remove the key or fix its spelling.");
        }

        var always = fm.Values("alwaysApply");
        if (fm.Has("alwaysApply") && !(always.Count == 1 && always[0] is "true" or "false"))
        {
            yield return new Finding(Id, fm.LineOf("alwaysApply"), "alwaysApply must be true or false.", "Set alwaysApply: true or alwaysApply: false.");
        }

        var alwaysOn = always is ["true"];
        if (!alwaysOn && fm.Values("globs").Count == 0 && fm.Values("description").Count == 0)
        {
            yield return new Finding(
                Id,
                1,
                "Rule never applies automatically: alwaysApply is not true, globs is empty, and description is empty.",
                "Set alwaysApply: true, add globs, or add a description the agent can match.");
        }
    }
}
