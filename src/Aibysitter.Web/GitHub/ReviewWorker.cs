namespace Aibysitter.Web.GitHub;

public sealed class ReviewWorker(ReviewQueue queue, ReviewProcessor processor) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.ReadAllAsync(stoppingToken))
        {
            await processor.ProcessAsync(job, stoppingToken);
        }
    }
}
