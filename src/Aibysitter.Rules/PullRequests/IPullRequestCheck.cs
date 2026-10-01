namespace Aibysitter.Rules.PullRequests;

public interface IPullRequestCheck
{
    string Id { get; }
    string Title { get; }
    Severity Severity { get; }
    IEnumerable<PullRequestFinding> Evaluate(PullRequestContext context);
}
