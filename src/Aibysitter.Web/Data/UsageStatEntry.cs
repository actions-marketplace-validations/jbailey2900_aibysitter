namespace Aibysitter.Web.Data;

/// <summary>Daily count for one metric and key. Row in <c>dbo.UsageStats</c>; no repository names, IPs or content.</summary>
public sealed class UsageStatEntry
{
    /// <summary>UTC date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>One of <see cref="Stats.UsageMetric.All"/>.</summary>
    public required string Metric { get; set; }

    public required string Key { get; set; }

    public int Count { get; set; }
}
