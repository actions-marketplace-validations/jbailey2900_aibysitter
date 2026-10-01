namespace Aibysitter.Rules;

public sealed record RulesLine(int Number, string Text, bool IsHeading, bool IsInCodeFence)
{
    public bool IsBlank => string.IsNullOrWhiteSpace(Text);

    /// <summary>Prose lines: not blank, not a heading, not inside or delimiting a code fence.</summary>
    public bool IsProse => !IsBlank && !IsHeading && !IsInCodeFence;
}
