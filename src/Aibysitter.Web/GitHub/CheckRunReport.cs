using System.Globalization;
using System.Text;
using Aibysitter.Rules;
using Aibysitter.Rules.PullRequests;

namespace Aibysitter.Web.GitHub;

public sealed record CheckRunAnnotation(string Path, int Line, Severity Severity, string Title, string Message, string RawDetails);

public sealed record CheckRunReport(ReviewConclusion Conclusion, string Title, string Summary, IReadOnlyList<CheckRunAnnotation> Annotations)
{
    public const int AnnotationsPerRequest = 50;

    public IEnumerable<IReadOnlyList<CheckRunAnnotation>> AnnotationBatches() => Annotations.Chunk(AnnotationsPerRequest);

    public static CheckRunReport Build(
        PullRequestReview review,
        IReadOnlyList<IPullRequestCheck> checks,
        IReadOnlyList<ChangedFile> files,
        RepoConfig config,
        IReadOnlyList<string> configErrors)
    {
        ArgumentNullException.ThrowIfNull(review);

        var checkById = checks.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var removed = files.Where(f => f.Status == FileChangeStatus.Removed).Select(f => f.Path).ToHashSet(StringComparer.Ordinal);

        var annotations = review.Findings
            .Where(f => !removed.Contains(f.Path))
            .Select(f => new CheckRunAnnotation(f.Path, f.Line, f.SeverityOr(checkById[f.CheckId].Severity), $"{f.CheckId} {checkById[f.CheckId].Title}", f.Message, f.FixHint))
            .ToList();

        var errors = review.Findings.Count(f => f.SeverityOr(checkById[f.CheckId].Severity) == Severity.Error);
        var warnings = review.Findings.Count(f => f.SeverityOr(checkById[f.CheckId].Severity) == Severity.Warning);
        var title = review.Findings.Count switch
        {
            0 => "No findings",
            1 => "1 finding",
            var n => $"{n} findings",
        };

        if (review.Findings.Count > 0)
        {
            title += $" ({errors} error{(errors == 1 ? "" : "s")}, {warnings} warning{(warnings == 1 ? "" : "s")})";
        }

        var summary = new StringBuilder();
        summary.AppendLine($"Conclusion mode: `{(config.Conclusion == ConclusionMode.FailOnErrors ? "fail-on-errors" : "advisory")}`. Scope: {(config.HasScope ? string.Join(", ", config.Scope.Select(g => $"`{g.Pattern}`")) : "not declared")}.");
        summary.AppendLine();
        summary.AppendLine("| Check | Severity | Findings |");
        summary.AppendLine("|---|---|---|");
        foreach (var check in checks)
        {
            summary.AppendLine($"| {check.Id} {check.Title} | {(check is RulesFileLint ? "Per rule" : check.Severity.ToString())} | {(config.IsEnabled(check.Id) ? review.Findings.Count(f => f.CheckId == check.Id).ToString(CultureInfo.InvariantCulture) : "disabled")} |");
        }

        var disabledRules = config.Disabled.Where(id => id.StartsWith('R')).Order(StringComparer.Ordinal).ToList();
        if (disabledRules.Count > 0 && config.IsEnabled(RulesFileLint.CheckId))
        {
            summary.AppendLine();
            summary.AppendLine($"Rules disabled for P014: {string.Join(", ", disabledRules)}.");
        }

        var onRemoved = review.Findings.Where(f => removed.Contains(f.Path)).ToList();
        if (onRemoved.Count > 0)
        {
            summary.AppendLine();
            summary.AppendLine("Findings on removed files:");
            foreach (var f in onRemoved)
            {
                summary.AppendLine($"- `{f.Path}`: {f.CheckId} {f.Message}");
            }
        }

        if (configErrors.Count > 0)
        {
            summary.AppendLine();
            summary.AppendLine($"Config errors (defaults used for these):");
            foreach (var error in configErrors)
            {
                summary.AppendLine($"- {error}");
            }
        }

        return new CheckRunReport(review.Conclusion, title, summary.ToString().TrimEnd(), annotations);
    }

    public static CheckRunReport ForError(Exception ex) => new(
        ReviewConclusion.Neutral,
        "Review failed",
        $"Aibysitter could not complete this review ({ex.GetType().Name}). Push a new commit or redeliver the webhook to retry.",
        []);
}
