namespace Aibysitter.Rules.PullRequests;

/// <summary>
/// A finding on a pull request. Line is on the head (new) side of the file.
/// Severity overrides the check's severity when set (P014 uses the rule's severity).
/// </summary>
public sealed record PullRequestFinding(string CheckId, string Path, int Line, string Message, string FixHint, Severity? Severity = null)
{
    public Severity SeverityOr(Severity checkSeverity) => Severity ?? checkSeverity;
}
