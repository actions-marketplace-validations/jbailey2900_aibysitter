using System.Text.RegularExpressions;

namespace Aibysitter.Rules;

/// <summary>
/// Inline suppression comments in a rules file:
/// <c>&lt;!-- aibysitter-disable R002 --&gt;</c> (whole file) and
/// <c>&lt;!-- aibysitter-disable-next-line R002, R005 --&gt;</c> (the following line).
/// Ignored inside code fences.
/// </summary>
public sealed partial class Suppressions
{
    public static Suppressions None { get; } = new(new HashSet<string>(StringComparer.OrdinalIgnoreCase), new Dictionary<int, HashSet<string>>());

    private readonly HashSet<string> fileWide;
    private readonly Dictionary<int, HashSet<string>> byLine;

    private Suppressions(HashSet<string> fileWide, Dictionary<int, HashSet<string>> byLine)
    {
        this.fileWide = fileWide;
        this.byLine = byLine;
    }

    public IReadOnlyCollection<string> FileWide => fileWide;

    public bool IsSuppressed(Finding finding) =>
        fileWide.Contains(finding.RuleId)
        || (byLine.TryGetValue(finding.Line, out var ids) && ids.Contains(finding.RuleId));

    /// <summary>True when the line is a suppression comment.</summary>
    public static bool IsDirective(string text) => DirectiveRegex().IsMatch(text);

    internal static Suppressions From(IEnumerable<RulesLine> lines)
    {
        HashSet<string>? fileWide = null;
        Dictionary<int, HashSet<string>>? byLine = null;

        foreach (var line in lines.Where(l => l.IsDirective))
        {
            var match = DirectiveRegex().Match(line.Text);
            var ids = IdRegex().Matches(match.Groups["ids"].Value).Select(m => m.Value.ToUpperInvariant());

            if (match.Groups["next"].Success)
            {
                byLine ??= [];
                if (!byLine.TryGetValue(line.Number + 1, out var set))
                {
                    byLine[line.Number + 1] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                set.UnionWith(ids);
            }
            else
            {
                (fileWide ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase)).UnionWith(ids);
            }
        }

        return fileWide is null && byLine is null
            ? None
            : new Suppressions(fileWide ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase), byLine ?? []);
    }

    [GeneratedRegex(@"^\s*<!--\s*aibysitter-disable(?<next>-next-line)?\s+(?<ids>R\d{3}(?:\s*,\s*R\d{3})*)\s*-->\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex DirectiveRegex();

    [GeneratedRegex(@"R\d{3}", RegexOptions.IgnoreCase)]
    private static partial Regex IdRegex();
}
