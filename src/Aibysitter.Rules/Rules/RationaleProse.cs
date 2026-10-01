using System.Text.RegularExpressions;

namespace Aibysitter.Rules.Rules;

public sealed partial class RationaleProse : PhraseRule
{
    public override string Id => "R001";
    public override string Title => "Rationale prose";
    public override Severity Severity => Severity.Info;

    protected override Regex Pattern => PhraseRegex();
    protected override string MessagePrefix => "Rationale prose";
    protected override string FixHint => "Remove the explanation. State the instruction only.";

    [GeneratedRegex(@"\b(?:because|so that|in order to|the reason|this ensures|this helps|which means)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PhraseRegex();
}
