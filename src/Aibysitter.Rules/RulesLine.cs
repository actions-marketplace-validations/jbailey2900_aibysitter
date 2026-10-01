namespace Aibysitter.Rules;

public sealed record RulesLine(int Number, string Text, bool IsHeading, bool IsInCodeFence)
{
    public bool IsBlank => string.IsNullOrWhiteSpace(Text);

    /// <summary>An <c>aibysitter-disable</c> suppression comment outside a code fence.</summary>
    public bool IsDirective => !IsInCodeFence && Suppressions.IsDirective(Text);

    /// <summary>Prose lines: not blank, not a heading, not a suppression comment, not inside or delimiting a code fence.</summary>
    public bool IsProse => !IsBlank && !IsHeading && !IsInCodeFence && !IsDirective;
}
