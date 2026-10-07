using Aibysitter.Rules.PullRequests;
using Aibysitter.Web.GitHub;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aibysitter.Rules.Tests.GitHub;

public sealed class ReviewWorkerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "aib-worker-" + Guid.NewGuid().ToString("N"));
    private readonly FakeGitHubGateway fake = new();
    private readonly ReviewQueue queue = new();
    private readonly ImmediateTime time = new();
    private readonly FileReviewJobStore store;
    private readonly ReviewJob job = new(new PullRequestRef(42, "o", "r", 7, "abcdef0123"), 777, Guid.NewGuid().ToString("D"));

    public ReviewWorkerTests()
    {
        store = new FileReviewJobStore(root, TimeProvider.System, NullLogger<FileReviewJobStore>.Instance);
    }

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public async Task CompletedReview_RemovesJob()
    {
        store.Save(job);

        await Worker().RunAsync(job, CancellationToken.None);

        Assert.Equal(new[] { "in_progress 777", "files", "content .github/aibysitter.json", "complete 777" }, fake.Calls);
        Assert.Equal(StoredJobState.None, store.Find(job.DeliveryId, out _));
    }

    [Fact]
    public async Task FailedReview_ClosedWithError_RemovesJob()
    {
        fake.ThrowOnFiles = new HttpRequestException("rate limited");
        store.Save(job);

        await Worker().RunAsync(job, CancellationToken.None);

        Assert.Equal("Review failed", (await fake.Completed.Task).Title);
        Assert.Equal(StoredJobState.None, store.Find(job.DeliveryId, out _));
    }

    [Fact]
    public async Task CheckRunNotClosed_KeepsJob_RequeuesAfterDelay()
    {
        fake.ThrowOnComplete = new HttpRequestException("404");
        store.Save(job);
        var worker = Worker();

        await worker.RunAsync(job, CancellationToken.None);
        await worker.PendingRetry!;

        Assert.Equal(StoredJobState.Pending, store.Find(job.DeliveryId, out _));
        Assert.Equal(new[] { job }, await Drain());
        Assert.Equal([ReviewWorker.RetryDelay], time.Delays);
    }

    [Fact]
    public async Task CheckRunNeverClosed_AbandonedAfterMaxAttempts_JobRemoved()
    {
        fake.ThrowOnComplete = new HttpRequestException("404");
        store.Save(job);
        var worker = Worker();

        for (var attempt = 1; attempt <= IReviewJobStore.MaxAttempts; attempt++)
        {
            await worker.RunAsync(job, CancellationToken.None);
            Assert.Equal(StoredJobState.Pending, store.Find(job.DeliveryId, out _));
        }

        await worker.RunAsync(job, CancellationToken.None);

        Assert.Equal(StoredJobState.None, store.Find(job.DeliveryId, out _));
        Assert.Equal(IReviewJobStore.MaxAttempts, fake.Calls.Count(c => c == "files"));
        Assert.Equal(IReviewJobStore.MaxAttempts * 2 + 1, fake.Calls.Count(c => c == "complete 777"));
    }

    [Fact]
    public async Task CheckRunNotClosed_InMemoryStore_NotRequeued()
    {
        fake.ThrowOnComplete = new HttpRequestException("404");
        var worker = new ReviewWorker(queue, new ReviewProcessor(fake, new PullRequestReviewer(), NullLogger<ReviewProcessor>.Instance), new InMemoryReviewJobStore(), fake, time, NullLogger<ReviewWorker>.Instance);

        await worker.RunAsync(job, CancellationToken.None);

        Assert.Null(worker.PendingRetry);
        Assert.Empty(time.Delays);
    }

    [Fact]
    public async Task Shutdown_DuringReview_KeepsJob()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        fake.ThrowOnFiles = new OperationCanceledException(cts.Token);
        store.Save(job);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Worker().RunAsync(job, cts.Token));

        Assert.Equal(StoredJobState.Pending, store.Find(job.DeliveryId, out _));
        Assert.DoesNotContain(fake.Calls, c => c.StartsWith("complete", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AfterMaxAttempts_ClosesCheckRunWithoutReviewing()
    {
        store.Save(job);
        for (var i = 0; i < IReviewJobStore.MaxAttempts; i++)
        {
            store.TryAcquire(job)!.Dispose();
        }

        await Worker().RunAsync(job, CancellationToken.None);

        Assert.Equal(new[] { "complete 777" }, fake.Calls);
        Assert.Equal(ReviewConclusion.Neutral, (await fake.Completed.Task).Conclusion);
        Assert.Equal(StoredJobState.None, store.Find(job.DeliveryId, out _));
    }

    [Fact]
    public async Task ThirdAttempt_StillReviews()
    {
        store.Save(job);
        for (var i = 0; i < IReviewJobStore.MaxAttempts - 1; i++)
        {
            store.TryAcquire(job)!.Dispose();
        }

        await Worker().RunAsync(job, CancellationToken.None);

        Assert.Contains("in_progress 777", fake.Calls);
    }

    [Fact]
    public async Task JobClaimedOrGone_Skipped()
    {
        await Worker().RunAsync(job, CancellationToken.None);

        store.Save(job);
        using var other = store.TryAcquire(job);
        await Worker().RunAsync(job, CancellationToken.None);

        Assert.Empty(fake.Calls);
    }

    [Fact]
    public async Task Recovery_QueuesSavedJobs_SkipsClaimed()
    {
        var claimed = job with { DeliveryId = Guid.NewGuid().ToString("D") };
        store.Save(job);
        store.Save(claimed);
        using var lease = store.TryAcquire(claimed);

        await RunRecovery(store);

        Assert.Equal(new[] { job }, await Drain());
    }

    [Fact]
    public async Task Recovery_InMemoryStore_QueuesNothing()
    {
        await RunRecovery(new InMemoryReviewJobStore());

        Assert.Empty(await Drain());
    }

    private ReviewWorker Worker() =>
        new(queue, new ReviewProcessor(fake, new PullRequestReviewer(), NullLogger<ReviewProcessor>.Instance), store, fake, time, NullLogger<ReviewWorker>.Instance);

    private async Task RunRecovery(IReviewJobStore source)
    {
        var recovery = new ReviewQueueRecovery(source, queue, NullLogger<ReviewQueueRecovery>.Instance);
        await recovery.StartAsync(CancellationToken.None);
        await recovery.ExecuteTask!;
    }

    private async Task<List<ReviewJob>> Drain()
    {
        var jobs = new List<ReviewJob>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        try
        {
            await foreach (var queued in queue.ReadAllAsync(cts.Token))
            {
                jobs.Add(queued);
            }
        }
        catch (OperationCanceledException)
        {
        }

        return jobs;
    }
}
