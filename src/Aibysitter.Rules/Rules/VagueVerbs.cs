using System.Text.RegularExpressions;

namespace Aibysitter.Rules.Rules;

public sealed partial class VagueVerbs : PhraseRule
{
    public override string Id => "R002";
    public override string Title => "Vague verbs";
    public override Severity Severity => Severity.Warning;

    protected override Regex Pattern => PhraseRegex();
    protected override string MessagePrefix => "Vague wording";
    protected override string FixHint => "Name the concrete action, file, or command.";

    [GeneratedRegex(@"\b(?:handle|manage|deal with|ensure|improve|optimize|clean up|properly|appropriate(?:ly)?|as needed)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PhraseRegex();
}
