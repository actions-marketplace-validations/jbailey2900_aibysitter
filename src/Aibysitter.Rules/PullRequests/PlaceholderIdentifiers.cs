using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

public sealed partial class PlaceholderIdentifiers : AddedLinePatternCheck
{
    public override string Id => "P001";
    public override string Title => "Placeholder identifiers";
    public override Severity Severity => Severity.Error;

    protected override Regex Pattern => PlaceholderRegex();
    protected override string MessagePrefix => "Placeholder left in change";
    protected override string FixHint => "Replace with the real identifier or value.";

    protected override bool AppliesTo(string path) => FileKinds.IsCodeOrConfig(path);

    [GeneratedRegex(@"::[A-Z][A-Z0-9_]*::|\bREPLACE_ME\b|\bYOUR_[A-Z0-9_]+\b|(?i:<placeholder>|<your[-_ ][^>]+>)|\bTODO_[A-Z0-9_]+\b")]
    private static partial Regex PlaceholderRegex();
}
