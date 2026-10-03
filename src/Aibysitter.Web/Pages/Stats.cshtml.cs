using Aibysitter.Web.Stats;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aibysitter.Web.Pages;

public class StatsModel(IUsageStats stats) : PageModel
{
    public bool Enabled => stats.Enabled;

    /// <summary>Null when stats are off or the database could not be read.</summary>
    public UsageSnapshot? Snapshot { get; private set; }

    public static readonly IReadOnlyDictionary<string, string> Headings = new Dictionary<string, string>
    {
        [UsageMetric.Lint] = "Lints by source",
        [UsageMetric.Finding] = "Findings by rule",
        [UsageMetric.Review] = "GitHub App reviews by conclusion",
        [UsageMetric.Badge] = "Badge requests by outcome",
    };

    public IEnumerable<UsageTotal> Totals(string metric) => Snapshot?.Totals.Where(t => t.Metric == metric) ?? [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (stats.Enabled)
        {
            Snapshot = await stats.ReadAsync(cancellationToken);
        }
    }
}
