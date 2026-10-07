using System.Reflection;

namespace Aibysitter.Rules.PullRequests;

public enum ReviewConclusion
{
    Success,
    Neutral,
    Failure,
}

public sealed record PullRequestReview(IReadOnlyList<PullRequestFinding> Findings, ReviewConclusion Conclusion);

public sealed class PullRequestReviewer
{
    public PullRequestReviewer()
        : this(DiscoverChecks())
    {
    }

    public PullRequestReviewer(IEnumerable<IPullRequestCheck> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);
        Checks = checks.OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<IPullRequestCheck> Checks { get; }

    public PullRequestReview Review(PullRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var findings = Checks
            .Where(c => context.Config.IsEnabled(c.Id))
            .SelectMany(c => c.Evaluate(ForCheck(c.Id, context)))
            .OrderBy(f => f.Path, StringComparer.Ordinal)
            .ThenBy(f => f.Line)
            .ThenBy(f => f.CheckId, StringComparer.Ordinal)
            .ToList();

        return new PullRequestReview(findings, Conclude(findings, context.Config));
    }

    /// <summary>The context with files that <c>ignore</c> excludes from this check removed.</summary>
    private static PullRequestContext ForCheck(string checkId, PullRequestContext context) =>
        context.Config.Ignore.Count == 0 || RepoConfig.PathOnlyChecks.Contains(checkId)
            ? context
            : context with { Files = context.Files.Where(f => !context.Config.IsIgnored(checkId, f.Path)).ToList() };

    private ReviewConclusion Conclude(IReadOnlyList<PullRequestFinding> findings, RepoConfig config)
    {
        if (findings.Count == 0)
        {
            return ReviewConclusion.Success;
        }

        if (config.Conclusion == ConclusionMode.FailOnErrors)
        {
            var severityByCheck = Checks.ToDictionary(c => c.Id, c => c.Severity, StringComparer.Ordinal);
            if (findings.Any(f => f.SeverityOr(severityByCheck[f.CheckId]) == Severity.Error))
            {
                return ReviewConclusion.Failure;
            }
        }

        return ReviewConclusion.Neutral;
    }

    public static IReadOnlyList<IPullRequestCheck> DiscoverChecks() =>
        typeof(IPullRequestCheck).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IPullRequestCheck).IsAssignableFrom(t))
            .Where(t => t.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) is not null)
            .Select(t => (IPullRequestCheck)Activator.CreateInstance(t)!)
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToList();
}
