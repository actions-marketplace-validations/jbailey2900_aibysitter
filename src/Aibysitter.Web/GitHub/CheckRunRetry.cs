using Octokit;

namespace Aibysitter.Web.GitHub;

/// <summary>
/// Check-run updates that return 404 are retried: GitHub sometimes answers 404 for a check run that exists
/// (seconds after it was created, or between two successful updates).
/// </summary>
internal static class CheckRunRetry
{
    public static readonly IReadOnlyList<TimeSpan> Delays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)];

    public static async Task RunAsync(Func<Task> update, long checkRunId, TimeProvider time, ILogger logger, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await update();
                return;
            }
            catch (NotFoundException) when (attempt < Delays.Count)
            {
                logger.LogWarning(
                    "Check run {CheckRunId} update returned 404; retry {Retry} of {MaxRetries} in {Delay}",
                    checkRunId, attempt + 1, Delays.Count, Delays[attempt]);
                await Task.Delay(Delays[attempt], time, cancellationToken);
            }
        }
    }
}
