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
            .SelectMany(c => c.Evaluate(context))
            .OrderBy(f => f.Path, StringComparer.Ordinal)
            .ThenBy(f => f.Line)
            .ThenBy(f => f.CheckId, StringComparer.Ordinal)
            .ToList();

        return new PullRequestReview(findings, Conclude(findings, context.Config));
    }

    private ReviewConclusion Conclude(IReadOnlyList<PullRequestFinding> findings, RepoConfig config)
    {
        if (findings.Count == 0)
        {
            return ReviewConclusion.Success;
        }

        if (config.Conclusion == ConclusionMode.FailOnErrors)
        {
            var errorChecks = Checks.Where(c => c.Severity == Severity.Error).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
            if (findings.Any(f => errorChecks.Contains(f.CheckId)))
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
