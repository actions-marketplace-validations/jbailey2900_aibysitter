namespace Aibysitter.Web.GitHub;

/// <summary>
/// Reviews queued jobs one at a time. Each review holds the job's lease; the job is removed once its check run is
/// completed and kept when shutdown interrupts it. After <see cref="IReviewJobStore.MaxAttempts"/> interrupted
/// attempts the check run is closed with an error instead.
/// </summary>
public sealed class ReviewWorker(
    ReviewQueue queue,
    ReviewProcessor processor,
    IReviewJobStore store,
    IGitHubGateway gateway,
    ILogger<ReviewWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.ReadAllAsync(stoppingToken))
        {
            await RunAsync(job, stoppingToken);
        }
    }

    internal async Task RunAsync(ReviewJob job, CancellationToken cancellationToken)
    {
        using var lease = store.TryAcquire(job);
        if (lease is null)
        {
            logger.LogInformation("Review of {PullRequest} (delivery {DeliveryId}) skipped: completed or claimed by another process", job.PullRequest, job.DeliveryId);
            return;
        }

        if (lease.Attempts > IReviewJobStore.MaxAttempts)
        {
            await AbandonAsync(job, cancellationToken);
        }
        else
        {
            await processor.ProcessAsync(job, cancellationToken);
        }

        lease.Complete();
    }

    private async Task AbandonAsync(ReviewJob job, CancellationToken cancellationToken)
    {
        logger.LogError("Review of {PullRequest} (delivery {DeliveryId}) interrupted {MaxAttempts} times; closing check run {CheckRunId}", job.PullRequest, job.DeliveryId, IReviewJobStore.MaxAttempts, job.CheckRunId);
        try
        {
            var error = new InvalidOperationException($"Review interrupted {IReviewJobStore.MaxAttempts} times");
            await gateway.CompleteCheckRunAsync(job.PullRequest, job.CheckRunId, CheckRunReport.ForError(error), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Could not close check run {CheckRunId} for {PullRequest}", job.CheckRunId, job.PullRequest);
        }
    }
}
