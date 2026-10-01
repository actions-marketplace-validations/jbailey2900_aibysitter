namespace Aibysitter.Rules.PullRequests;

/// <summary>
/// Runs the rules-file rules (R001–R015) on rules files the pull request adds or changes, using head content.
/// Added files: every finding. Changed files: findings on added lines, plus whole-file rules.
/// Precedence: in-file aibysitter-disable comments, then rule IDs in config "disable", then P014 disabled.
/// Each finding carries its rule's severity.
/// </summary>
public sealed class RulesFileLint : IPullRequestCheck
{
    public const string CheckId = "P014";

    /// <summary>Rules whose finding describes the whole file; reported for changed files regardless of the line.</summary>
    public static readonly IReadOnlySet<string> WholeFileRules = new HashSet<string>(StringComparer.Ordinal) { "R004", "R008", "R012", "R015" };

    private readonly LintEngine engine;
    private readonly Dictionary<string, IRule> rules;

    public RulesFileLint()
        : this(new LintEngine())
    {
    }

    public RulesFileLint(LintEngine engine)
    {
        this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
        rules = engine.Rules.ToDictionary(r => r.Id, StringComparer.Ordinal);
    }

    public string Id => CheckId;
    public string Title => "Rules file lint";

    /// <summary>Nominal severity for docs and fallbacks; each finding sets its own.</summary>
    public Severity Severity => Severity.Warning;

    public static bool IsRulesFile(ChangedFile file) =>
        file.Status != FileChangeStatus.Removed && RulesFormats.FromFileName(file.Path) is not null;

    public IEnumerable<PullRequestFinding> Evaluate(PullRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var file in context.Files.Where(f => IsRulesFile(f) && f.HeadContent is not null))
        {
            var result = engine.Analyze(file.HeadContent!, RulesFormats.FromFileName(file.Path)!.Value);
            var added = file.AddedLines.Select(l => l.NewLine!.Value).ToHashSet();
            var isNew = file.Status == FileChangeStatus.Added;

            foreach (var finding in result.Findings.Where(f => context.Config.IsEnabled(f.RuleId)))
            {
                if (!isNew && !added.Contains(finding.Line) && !WholeFileRules.Contains(finding.RuleId))
                {
                    continue;
                }

                var rule = rules[finding.RuleId];
                yield return new PullRequestFinding(
                    Id,
                    file.Path,
                    finding.Line,
                    $"{finding.RuleId} {rule.Title}: {finding.Message}",
                    finding.FixHint,
                    rule.Severity);
            }
        }
    }
}
