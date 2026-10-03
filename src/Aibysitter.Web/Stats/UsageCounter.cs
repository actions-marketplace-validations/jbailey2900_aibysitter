using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Aibysitter.Web.Stats;

public static class UsageMetric
{
    public const string Lint = "lint";
    public const string Finding = "finding";
    public const string Review = "review";
    public const string Badge = "badge";

    public static readonly IReadOnlyList<string> All = [Lint, Finding, Review, Badge];

    /// <summary>Lint sources counted under <see cref="Lint"/>; badge lints are counted under <see cref="Badge"/> only.</summary>
    public static readonly IReadOnlyList<string> LintSources = ["form", "api", "url"];
}

public readonly record struct UsageKey(DateOnly Date, string Metric, string Key);

/// <summary>Counts events in memory; <see cref="UsageStatsFlusher"/> writes them as daily totals.</summary>
public interface IUsageCounter
{
    void Increment(string metric, string key, int by = 1);
}

public sealed class NullUsageCounter : IUsageCounter
{
    public void Increment(string metric, string key, int by = 1)
    {
    }
}

public sealed class UsageCounter(TimeProvider time) : IUsageCounter
{
    private readonly ConcurrentDictionary<UsageKey, StrongBox<int>> counts = new();

    public void Increment(string metric, string key, int by = 1)
    {
        var box = counts.GetOrAdd(new UsageKey(DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime), metric, key), _ => new StrongBox<int>());
        Interlocked.Add(ref box.Value, by);
    }

    /// <summary>Takes every non-zero count and resets it to zero. Keys older than yesterday are removed.</summary>
    public IReadOnlyDictionary<UsageKey, int> Drain()
    {
        var taken = new Dictionary<UsageKey, int>();
        foreach (var (key, box) in counts)
        {
            var value = Interlocked.Exchange(ref box.Value, 0);
            if (value != 0)
            {
                taken[key] = value;
            }
        }

        var yesterday = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime).AddDays(-1);
        foreach (var old in counts.Keys.Where(k => k.Date < yesterday))
        {
            if (counts.TryRemove(old, out var box) && box.Value != 0)
            {
                taken[old] = taken.GetValueOrDefault(old) + Interlocked.Exchange(ref box.Value, 0);
            }
        }

        return taken;
    }

    /// <summary>Adds counts back after a failed flush.</summary>
    public void Restore(IReadOnlyDictionary<UsageKey, int> deltas)
    {
        foreach (var (key, value) in deltas)
        {
            Interlocked.Add(ref counts.GetOrAdd(key, _ => new StrongBox<int>()).Value, value);
        }
    }
}
