using Aibysitter.Rules.PullRequests;

namespace Aibysitter.Web.GitHub;

public sealed class ReviewProcessor(IGitHubGateway gateway, PullRequestReviewer reviewer, ILogger<ReviewProcessor> logger)
{
    public const int MaxContentFetches = 100;

    public async Task ProcessAsync(ReviewJob job, CancellationToken cancellationToken)
    {
        var pr = job.PullRequest;
        try
        {
            await gateway.MarkInProgressAsync(pr, job.CheckRunId, cancellationToken);

            var files = await gateway.GetChangedFilesAsync(pr, cancellationToken);
            var (config, configErrors) = RepoConfig.Parse(await gateway.GetFileContentAsync(pr, RepoConfig.FilePath, cancellationToken));

            var enriched = new List<ChangedFile>(files.Count);
            var fetched = 0;
            foreach (var file in files)
            {
                if (NeedsHeadContent(file) && fetched < MaxContentFetches)
                {
                    fetched++;
                    enriched.Add(file with { HeadContent = await gateway.GetFileContentAsync(pr, file.Path, cancellationToken) });
                }
                else
                {
                    enriched.Add(file);
                }
            }

            var review = reviewer.Review(new PullRequestContext(enriched, config));
            var report = CheckRunReport.Build(review, reviewer.Checks, enriched, config, configErrors);
            await gateway.CompleteCheckRunAsync(pr, job.CheckRunId, report, cancellationToken);

            logger.LogInformation(
                "Reviewed {PullRequest} (delivery {DeliveryId}): {FindingCount} findings, {Conclusion}",
                pr, job.DeliveryId, review.Findings.Count, review.Conclusion);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Review of {PullRequest} (delivery {DeliveryId}) failed", pr, job.DeliveryId);
            try
            {
                await gateway.CompleteCheckRunAsync(pr, job.CheckRunId, CheckRunReport.ForError(ex), cancellationToken);
            }
            catch (Exception closeEx)
            {
                logger.LogError(closeEx, "Could not close check run {CheckRunId} for {PullRequest}", job.CheckRunId, pr);
            }
        }
    }

    internal static bool NeedsHeadContent(ChangedFile file) =>
        file.Status != FileChangeStatus.Removed
        && file.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
        && (file.Path.Contains("test", StringComparison.OrdinalIgnoreCase) || (file.Patch?.Contains('[') ?? false));
}
