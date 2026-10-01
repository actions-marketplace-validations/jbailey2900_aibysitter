using Aibysitter.Rules.PullRequests;

namespace Aibysitter.Web.GitHub;

public interface IGitHubGateway
{
    Task<long> CreateQueuedCheckRunAsync(PullRequestRef pr, CancellationToken cancellationToken);

    Task MarkInProgressAsync(PullRequestRef pr, long checkRunId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChangedFile>> GetChangedFilesAsync(PullRequestRef pr, CancellationToken cancellationToken);

    /// <summary>File text at the PR head commit, or null when the file does not exist.</summary>
    Task<string?> GetFileContentAsync(PullRequestRef pr, string path, CancellationToken cancellationToken);

    Task CompleteCheckRunAsync(PullRequestRef pr, long checkRunId, CheckRunReport report, CancellationToken cancellationToken);
}
