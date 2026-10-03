using Aibysitter.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Aibysitter.Web.Stats;

/// <summary>
/// Every <see cref="Interval"/> and on shutdown: adds the counted deltas to <c>dbo.UsageStats</c> in one transaction.
/// A failed flush keeps the deltas; after <see cref="MaxFailures"/> failures in a row they are dropped.
/// </summary>
public sealed class UsageStatsFlusher(UsageCounter counter, IDbContextFactory<AibysitterDbContext> contexts, ILogger<UsageStatsFlusher> logger, TimeProvider time) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);
    public const int MaxFailures = 10;

    private int failures;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await FlushAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await FlushAsync(cancellationToken);
    }

    /// <summary>Returns the number of rows written; 0 when there was nothing to write or the write failed.</summary>
    public async Task<int> FlushAsync(CancellationToken cancellationToken)
    {
        var deltas = counter.Drain();
        if (deltas.Count == 0)
        {
            return 0;
        }

        try
        {
            await using var db = await contexts.CreateDbContextAsync(cancellationToken);
            await UpsertAsync(db, deltas, cancellationToken);
            failures = 0;
            return deltas.Count;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (++failures < MaxFailures)
            {
                counter.Restore(deltas);
                logger.LogWarning("Usage stats flush failed ({Failures} in a row): {Error}", failures, ex.GetType().Name);
            }
            else
            {
                logger.LogWarning("Usage stats flush failed {Failures} times in a row; {Count} counts dropped: {Error}", failures, deltas.Count, ex.GetType().Name);
                failures = 0;
            }

            return 0;
        }
    }

    public static async Task UpsertAsync(AibysitterDbContext db, IReadOnlyDictionary<UsageKey, int> deltas, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            foreach (var (key, delta) in deltas)
            {
                await db.Database.ExecuteSqlAsync(
                    $"""
                    UPDATE [dbo].[UsageStats] WITH (UPDLOCK, SERIALIZABLE) SET [Count] = [Count] + {delta}
                    WHERE [Date] = {key.Date} AND [Metric] = {key.Metric} AND [Key] = {key.Key};
                    IF @@ROWCOUNT = 0
                        INSERT [dbo].[UsageStats] ([Date], [Metric], [Key], [Count]) VALUES ({key.Date}, {key.Metric}, {key.Key}, {delta});
                    """,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        });
    }
}
