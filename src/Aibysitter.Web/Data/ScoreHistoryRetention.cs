using Microsoft.EntityFrameworkCore;

namespace Aibysitter.Web.Data;

/// <summary>Daily: deletes rows older than <see cref="MaxAge"/>, then all but the newest <see cref="MaxRowsPerFile"/> per repository and file. Batches of <see cref="BatchSize"/>.</summary>
public sealed class ScoreHistoryRetention(IDbContextFactory<AibysitterDbContext> contexts, ILogger<ScoreHistoryRetention> logger, TimeProvider time) : BackgroundService
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(400);
    public const int MaxRowsPerFile = 500;
    public const int BatchSize = 5000;

    private static readonly TimeSpan FirstRun = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstRun, time, stoppingToken);
            using var timer = new PeriodicTimer(Interval, time);
            do
            {
                try
                {
                    await using var db = await contexts.CreateDbContextAsync(stoppingToken);
                    var deleted = await PurgeAsync(db, time.GetUtcNow().UtcDateTime, stoppingToken);
                    logger.LogInformation("Score history retention deleted {Count} rows", deleted);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning("Score history retention failed: {Error}", ex.GetType().Name);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>Applies both limits; returns the number of rows deleted.</summary>
    public static async Task<int> PurgeAsync(AibysitterDbContext db, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var cutoff = nowUtc - MaxAge;
        var total = 0;
        int deleted;
        do
        {
            deleted = await db.Database.ExecuteSqlAsync(
                $"DELETE TOP ({BatchSize}) FROM [dbo].[ScoreHistory] WHERE [CreatedUtc] < {cutoff}", cancellationToken);
            total += deleted;
        }
        while (deleted == BatchSize);

        do
        {
            deleted = await db.Database.ExecuteSqlAsync(
                $"""
                WITH ranked AS (
                    SELECT ROW_NUMBER() OVER (PARTITION BY [Repo], [FileName] ORDER BY [CreatedUtc] DESC, [Id] DESC) AS rn
                    FROM [dbo].[ScoreHistory])
                DELETE TOP ({BatchSize}) FROM ranked WHERE rn > {MaxRowsPerFile}
                """, cancellationToken);
            total += deleted;
        }
        while (deleted == BatchSize);

        return total;
    }
}
