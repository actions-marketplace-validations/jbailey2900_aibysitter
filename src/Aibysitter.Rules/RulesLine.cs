namespace Aibysitter.Rules;

public sealed record RulesLine(int Number, string Text, bool IsHeading, bool IsInCodeFence, bool IsFrontmatter = false)
{
    public bool IsBlank => string.IsNullOrWhiteSpace(Text);

    /// <summary>An <c>aibysitter-disable</c> suppression comment outside a code fence.</summary>
    public bool IsDirective => !IsInCodeFence && !IsFrontmatter && Suppressions.IsDirective(Text);

    /// <summary>Prose lines: not blank, not a heading, not frontmatter, not a suppression comment, not inside or delimiting a code fence.</summary>
    public bool IsProse => !IsBlank && !IsHeading && !IsInCodeFence && !IsFrontmatter && !IsDirective;
}
