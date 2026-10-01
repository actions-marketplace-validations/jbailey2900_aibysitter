using System.Reflection;

namespace Aibysitter.Rules;

public sealed class LintEngine
{
    public LintEngine()
        : this(DiscoverRules())
    {
    }

    public LintEngine(IEnumerable<IRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Rules = rules.OrderBy(r => r.Id, StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<IRule> Rules { get; }

    public IReadOnlyList<Finding> Lint(string text) => Lint(RulesFile.Parse(text));

    public IReadOnlyList<Finding> Lint(RulesFile file) => Analyze(file).Findings;

    public LintResult Analyze(string text) => Analyze(RulesFile.Parse(text));

    /// <summary>Runs every rule. Findings matched by an <c>aibysitter-disable</c> comment go to <see cref="LintResult.Suppressed"/>.</summary>
    public LintResult Analyze(RulesFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var all = Rules
            .SelectMany(r => r.Evaluate(file))
            .OrderBy(f => f.Line)
            .ThenBy(f => f.RuleId, StringComparer.Ordinal)
            .ToList();

        var suppressed = all.Where(file.Suppressions.IsSuppressed).ToList();
        return suppressed.Count == 0
            ? new LintResult(all, [])
            : new LintResult(all.Where(f => !file.Suppressions.IsSuppressed(f)).ToList(), suppressed);
    }

    public LintScore Score(IReadOnlyList<Finding> findings) =>
        Scorer.Score(findings, Rules.ToDictionary(r => r.Id, r => r.Severity, StringComparer.Ordinal));

    public static IReadOnlyList<IRule> DiscoverRules() =>
        typeof(IRule).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IRule).IsAssignableFrom(t))
            .Where(t => t.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) is not null)
            .Select(t => (IRule)Activator.CreateInstance(t)!)
            .OrderBy(r => r.Id, StringComparer.Ordinal)
            .ToList();
}
