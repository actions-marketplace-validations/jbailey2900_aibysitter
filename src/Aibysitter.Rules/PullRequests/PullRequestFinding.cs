namespace Aibysitter.Rules.PullRequests;

/// <summary>A finding on a pull request. Line is on the head (new) side of the file.</summary>
public sealed record PullRequestFinding(string CheckId, string Path, int Line, string Message, string FixHint);
