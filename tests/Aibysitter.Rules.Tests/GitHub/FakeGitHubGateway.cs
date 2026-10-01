using System.Collections.Concurrent;
using Aibysitter.Rules.PullRequests;
using Aibysitter.Web.GitHub;

namespace Aibysitter.Rules.Tests.GitHub;

internal sealed class FakeGitHubGateway : IGitHubGateway
{
    public ConcurrentQueue<string> Calls { get; } = new();

    public List<ChangedFile> Files { get; } = [];

    public Dictionary<string, string> Contents { get; } = new(StringComparer.Ordinal);

    public long NextCheckRunId { get; set; } = 777;

    public Exception? ThrowOnCreate { get; set; }

    public Exception? ThrowOnFiles { get; set; }

    public Exception? ThrowOnComplete { get; set; }

    public TaskCompletionSource<CheckRunReport> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<long> CreateQueuedCheckRunAsync(PullRequestRef pr, CancellationToken cancellationToken)
    {
        Calls.Enqueue($"create {pr}");
        return ThrowOnCreate is null ? Task.FromResult(NextCheckRunId) : Task.FromException<long>(ThrowOnCreate);
    }

    public Task MarkInProgressAsync(PullRequestRef pr, long checkRunId, CancellationToken cancellationToken)
    {
        Calls.Enqueue($"in_progress {checkRunId}");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ChangedFile>> GetChangedFilesAsync(PullRequestRef pr, CancellationToken cancellationToken)
    {
        Calls.Enqueue("files");
        return ThrowOnFiles is null ? Task.FromResult<IReadOnlyList<ChangedFile>>(Files) : Task.FromException<IReadOnlyList<ChangedFile>>(ThrowOnFiles);
    }

    public Task<string?> GetFileContentAsync(PullRequestRef pr, string path, CancellationToken cancellationToken)
    {
        Calls.Enqueue($"content {path}");
        return Task.FromResult(Contents.GetValueOrDefault(path));
    }

    public Task CompleteCheckRunAsync(PullRequestRef pr, long checkRunId, CheckRunReport report, CancellationToken cancellationToken)
    {
        Calls.Enqueue($"complete {checkRunId}");
        if (ThrowOnComplete is not null)
        {
            Completed.TrySetException(ThrowOnComplete);
            return Task.FromException(ThrowOnComplete);
        }

        Completed.TrySetResult(report);
        return Task.CompletedTask;
    }
}
