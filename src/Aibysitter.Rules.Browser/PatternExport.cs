using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aibysitter.Rules.Browser;

/// <summary>
/// Builds <c>rules-patterns.mjs</c>: every [GeneratedRegex] in the lint-page rule types, translated for JavaScript,
/// plus rule metadata, scoring constants, and format names. Pull-request and repository types are not exported.
/// </summary>
public static class PatternExport
{
    private static readonly string[] Namespaces = ["Aibysitter.Rules", "Aibysitter.Rules.Rules"];

    public static IReadOnlyDictionary<string, (string Source, string Flags)> Patterns()
    {
        var result = new SortedDictionary<string, (string, string)>(StringComparer.Ordinal);
        foreach (var type in typeof(LintEngine).Assembly.GetTypes().Where(t => Namespaces.Contains(t.Namespace) && !t.IsNested))
        {
            foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (method.GetCustomAttribute<GeneratedRegexAttribute>() is { } attr)
                {
                    result[$"{type.Name}.{method.Name}"] = JsRegexTranslator.Translate(attr.Pattern, attr.Options);
                }
            }
        }

        return result;
    }

    /// <summary>Each grade with the lowest score that earns it, highest grade first.</summary>
    private static IEnumerable<object> Grades() =>
        Enumerable.Range(0, Scorer.MaxScore + 1)
            .GroupBy(Scorer.Grade)
            .Select(g => new { grade = g.Key, min = g.Min() })
            .OrderByDescending(g => g.min);

    public static string Build()
    {
        var engine = new LintEngine();
        var data = new
        {
            rulesetVersion = RulesetVersion.Current,
            patterns = Patterns().ToDictionary(p => p.Key, p => new { source = p.Value.Source, flags = p.Value.Flags }),
            rules = engine.Rules.Select(r => new { id = r.Id, title = r.Title, severity = r.Severity.ToString() }),
            scoring = new
            {
                maxScore = Scorer.MaxScore,
                perRuleCap = Scorer.PerRuleCap,
                weights = Enum.GetValues<Severity>().ToDictionary(s => s.ToString(), Scorer.Weight),
                caps = Enum.GetValues<Severity>().ToDictionary(s => s.ToString(), Scorer.SeverityCap),
                severityOrder = Enum.GetValues<Severity>().Select(s => s.ToString()),
                grades = Grades(),
            },
            formats = new
            {
                names = Enum.GetValues<RulesFormat>().ToDictionary(f => f.ToString(), RulesFormats.DisplayName),
                cursorKeys = RulesFormats.CursorKeys.Order(StringComparer.Ordinal),
            },
            limits = new
            {
                fileMaxLines = Rules.FileLength.MaxLines,
                duplicateMinWords = Rules.DuplicateLines.MinWords,
                paragraphMaxWords = Rules.ProseParagraph.MaxWords,
                headingInstructionMinWords = Rules.EmptySections.MinInstructionWords,
                emphasisPerHundred = Rules.EmphasisInflation.PerHundredLines,
                emphasisMinAllowed = Rules.EmphasisInflation.MinAllowed,
                frontmatterMaxLines = Frontmatter.MaxLines,
            },
        };

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        return "// Generated at build time by Aibysitter.Rules.Browser from the C# rule definitions. Do not edit.\n"
            + $"export default {json};\n";
    }
}
