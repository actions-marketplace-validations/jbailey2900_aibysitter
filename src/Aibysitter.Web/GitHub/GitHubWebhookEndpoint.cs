using Microsoft.Extensions.Options;

namespace Aibysitter.Web.GitHub;

public static class GitHubWebhookEndpoint
{
    public const string Path = "/github/webhook";
    public const int MaxBodyBytes = 25 * 1024 * 1024;

    public static IEndpointRouteBuilder MapGitHubWebhook(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Path, HandleAsync).DisableAntiforgery();
        return endpoints;
    }

    internal static async Task<IResult> HandleAsync(
        HttpRequest request,
        IOptions<GitHubOptions> options,
        IGitHubGateway gateway,
        ReviewQueue queue,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(GitHubWebhookEndpoint));
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            logger.LogWarning("GitHub webhook received but the GitHub App is not configured");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (request.ContentLength > MaxBodyBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, cancellationToken);
        var body = buffer.ToArray();

        if (!WebhookSignature.IsValid(settings.WebhookSecret!, body, request.Headers[WebhookSignature.HeaderName]))
        {
            logger.LogWarning("GitHub webhook rejected: invalid signature");
            return Results.Unauthorized();
        }

        var eventName = request.Headers["X-GitHub-Event"].ToString();
        var deliveryId = request.Headers["X-GitHub-Delivery"].ToString();

        if (eventName == "ping")
        {
            return Results.Ok("pong");
        }

        if (eventName != "pull_request")
        {
            return Results.NoContent();
        }

        if (!WebhookPayload.TryParsePullRequest(body, out var action, out var pr))
        {
            logger.LogWarning("GitHub delivery {DeliveryId}: pull_request payload missing required fields", deliveryId);
            return Results.BadRequest();
        }

        if (!WebhookPayload.ReviewedActions.Contains(action))
        {
            return Results.NoContent();
        }

        long checkRunId;
        try
        {
            checkRunId = await gateway.CreateQueuedCheckRunAsync(pr!, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GitHub delivery {DeliveryId}: could not create queued check run for {PullRequest}", deliveryId, pr);
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }

        if (!queue.TryEnqueue(new ReviewJob(pr!, checkRunId, deliveryId)))
        {
            logger.LogError("GitHub delivery {DeliveryId}: review queue full; closing check run {CheckRunId}", deliveryId, checkRunId);
            try
            {
                await gateway.CompleteCheckRunAsync(pr!, checkRunId, CheckRunReport.ForError(new InvalidOperationException("Review queue full")), cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "GitHub delivery {DeliveryId}: could not close check run {CheckRunId}", deliveryId, checkRunId);
            }

            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        logger.LogInformation("GitHub delivery {DeliveryId}: queued review of {PullRequest} as check run {CheckRunId}", deliveryId, pr, checkRunId);
        return Results.Accepted();
    }
}
