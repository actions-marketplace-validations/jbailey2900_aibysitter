namespace Aibysitter.Web.GitHub;

/// <summary>On start, queues the jobs a previous process saved and did not complete.</summary>
public sealed class ReviewQueueRecovery(IReviewJobStore store, ReviewQueue queue, ILogger<ReviewQueueRecovery> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!store.IsDurable)
        {
            logger.LogWarning("ReviewQueue:Path is not set; review jobs are kept in memory and lost on restart");
            return;
        }

        var pending = store.LoadPending();
        foreach (var job in pending)
        {
            await queue.EnqueueAsync(job, stoppingToken);
        }

        logger.LogInformation("Recovered {Count} saved review jobs", pending.Count);
    }
}
