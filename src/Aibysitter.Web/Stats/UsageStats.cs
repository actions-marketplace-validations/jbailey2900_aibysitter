using Aibysitter.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Aibysitter.Web.Stats;

public sealed record UsageTotal(string Metric, string Key, long Last30Days, long AllTime);

/// <param name="ByMetric">Total per metric that day; missing metrics are 0.</param>
public sealed record UsageDay(DateOnly Date, IReadOnlyDictionary<string, long> ByMetric)
{
    public long Of(string metric) => ByMetric.GetValueOrDefault(metric);
}

/// <param name="Days">The last <see cref="IUsageStats.Days"/> days, newest first, including days with no counts.</param>
public sealed record UsageSnapshot(IReadOnlyList<UsageTotal> Totals, IReadOnlyList<UsageDay> Days);

public interface IUsageStats
{
    public const int Days = 30;

    bool Enabled { get; }

    /// <summary>Null when the database could not be read.</summary>
    Task<UsageSnapshot?> ReadAsync(CancellationToken cancellationToken);
}

public sealed class NullUsageStats : IUsageStats
{
    public bool Enabled => false;

    public Task<UsageSnapshot?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult<UsageSnapshot?>(null);
}

/// <summary>Reads <c>dbo.UsageStats</c>; results are cached for <see cref="CacheFor"/>.</summary>
public sealed class EfUsageStats(IDbContextFactory<AibysitterDbContext> contexts, TimeProvider time, ILogger<EfUsageStats> logger) : IUsageStats
{
    public static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim gate = new(1, 1);
    private (UsageSnapshot Snapshot, DateTimeOffset Until)? cached;

    public bool Enabled => true;

    public async Task<UsageSnapshot?> ReadAsync(CancellationToken cancellationToken)
    {
        if (cached is { } hit && hit.Until > time.GetUtcNow())
        {
            return hit.Snapshot;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (cached is { } again && again.Until > time.GetUtcNow())
            {
                return again.Snapshot;
            }

            await using var db = await contexts.CreateDbContextAsync(cancellationToken);
            var snapshot = await QueryAsync(db, DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime), cancellationToken);
            cached = (snapshot, time.GetUtcNow() + CacheFor);
            return snapshot;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Usage stats read failed: {Error}", ex.GetType().Name);
            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    public static async Task<UsageSnapshot> QueryAsync(AibysitterDbContext db, DateOnly today, CancellationToken cancellationToken)
    {
        var from = today.AddDays(1 - IUsageStats.Days);
        var totals = await db.UsageStats
            .GroupBy(e => new { e.Metric, e.Key })
            .Select(g => new UsageTotal(g.Key.Metric, g.Key.Key, g.Sum(e => e.Date >= from ? (long)e.Count : 0L), g.Sum(e => (long)e.Count)))
            .ToListAsync(cancellationToken);

        var daily = await db.UsageStats
            .Where(e => e.Date >= from)
            .GroupBy(e => new { e.Date, e.Metric })
            .Select(g => new { g.Key.Date, g.Key.Metric, Count = g.Sum(e => (long)e.Count) })
            .ToListAsync(cancellationToken);

        var days = Enumerable.Range(0, IUsageStats.Days)
            .Select(i => today.AddDays(-i))
            .Select(d => new UsageDay(d, daily.Where(r => r.Date == d).ToDictionary(r => r.Metric, r => r.Count, StringComparer.Ordinal)))
            .ToList();

        return new UsageSnapshot(
            totals.OrderBy(t => UsageMetricOrder(t.Metric)).ThenByDescending(t => t.AllTime).ThenBy(t => t.Key, StringComparer.Ordinal).ToList(),
            days);
    }

    private static int UsageMetricOrder(string metric) => UsageMetric.All.ToList().IndexOf(metric);
}
