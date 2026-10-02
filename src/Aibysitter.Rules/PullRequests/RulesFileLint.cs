using Aibysitter.Rules.Repo;
using Aibysitter.Rules.Rules;

namespace Aibysitter.Rules.PullRequests;

/// <summary>
/// Runs the rules-file rules (R001–R016) on rules files the pull request adds or changes, using head content.
/// Added files: every finding. Changed files: findings on added lines, plus whole-file rules.
/// Precedence: in-file aibysitter-disable comments, then rule IDs in config "disable", then P014 disabled.
/// Each finding carries its rule's severity.
/// R006 (with <see cref="PullRequestContext.Repo"/>): every reference in changed rules files; in unchanged rules files,
/// only references broken by paths or scripts this PR removes or renames.
/// </summary>
public sealed class RulesFileLint : IPullRequestCheck
{
    public const string CheckId = "P014";

    /// <summary>Rules whose finding describes the whole file; reported for changed files regardless of the line.</summary>
    public static readonly IReadOnlySet<string> WholeFileRules = new HashSet<string>(StringComparer.Ordinal) { "R004", "R008", "R012", "R015", "R016" };

    private readonly LintEngine engine;
    private readonly Dictionary<string, IRule> rules;
    private readonly MissingIdentifiers missingIdentifiers = new();

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

        var checkR006 = context.Repo is not null && context.Config.IsEnabled(MissingIdentifiers.RuleId);

        foreach (var file in context.Files.Where(f => IsRulesFile(f) && f.HeadContent is not null))
        {
            var parsed = RulesFile.Parse(file.HeadContent!, RulesFormats.FromFileName(file.Path)!.Value);
            var result = engine.Analyze(parsed);
            var added = file.AddedLines.Select(l => l.NewLine!.Value).ToHashSet();
            var isNew = file.Status == FileChangeStatus.Added;

            foreach (var finding in result.Findings.Where(f => context.Config.IsEnabled(f.RuleId)))
            {
                if (!isNew && !added.Contains(finding.Line) && !WholeFileRules.Contains(finding.RuleId))
                {
                    continue;
                }

                var rule = rules[finding.RuleId];
                yield return ToFinding(file.Path, finding, rule.Title, rule.Severity);
            }

            if (checkR006)
            {
                foreach (var finding in missingIdentifiers.Evaluate(parsed, file.Path, context.Repo!).Where(f => !parsed.Suppressions.IsSuppressed(f)))
                {
                    yield return ToFinding(file.Path, finding, missingIdentifiers.Title, missingIdentifiers.Severity);
                }
            }
        }

        if (!checkR006 || context.UnchangedRulesFiles is not { Count: > 0 } unchanged)
        {
            yield break;
        }

        var removed = RemovedIdentifiers.From(context.Files);
        if (!removed.Any)
        {
            yield break;
        }

        foreach (var file in unchanged.Where(f => f.HeadContent is not null && RulesFormats.FromFileName(f.Path) is not null))
        {
            var parsed = RulesFile.Parse(file.HeadContent!, RulesFormats.FromFileName(file.Path)!.Value);
            foreach (var finding in missingIdentifiers.Evaluate(parsed, file.Path, context.Repo!, removed).Where(f => !parsed.Suppressions.IsSuppressed(f)))
            {
                yield return ToFinding(file.Path, finding, missingIdentifiers.Title, missingIdentifiers.Severity);
            }
        }
    }

    private PullRequestFinding ToFinding(string path, Finding finding, string title, Severity severity) =>
        new(Id, path, finding.Line, $"{finding.RuleId} {title}: {finding.Message}", finding.FixHint, severity);
}
