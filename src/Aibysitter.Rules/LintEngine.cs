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

    public IReadOnlyList<Finding> Lint(RulesFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return Rules
            .SelectMany(r => r.Evaluate(file))
            .OrderBy(f => f.Line)
            .ThenBy(f => f.RuleId, StringComparer.Ordinal)
            .ToList();
    }

    public static IReadOnlyList<IRule> DiscoverRules() =>
        typeof(IRule).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IRule).IsAssignableFrom(t))
            .Where(t => t.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) is not null)
            .Select(t => (IRule)Activator.CreateInstance(t)!)
            .OrderBy(r => r.Id, StringComparer.Ordinal)
            .ToList();
}
