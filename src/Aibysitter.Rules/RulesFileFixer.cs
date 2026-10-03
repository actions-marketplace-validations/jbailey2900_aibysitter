namespace Aibysitter.Rules;

/// <param name="Text">The fixed text: same line endings, BOM and trailing newline as the input.</param>
/// <param name="Fixed">Findings fixed, in the order applied; line numbers are as found in each pass.</param>
public sealed record FixResult(string Text, IReadOnlyList<Finding> Fixed);

/// <summary>
/// Mechanical fixes, repeated until none apply (at most <see cref="MaxPasses"/> passes):
/// R012 appends a closing fence matching the opening at end of file; R005 deletes the repeated line;
/// R011 deletes the empty heading and the blank lines after it. Suppressed findings and disabled rules are not fixed.
/// </summary>
public static class RulesFileFixer
{
    public const int MaxPasses = 5;
    public const string DuplicateLines = "R005";
    public const string EmptySections = "R011";
    public const string UnclosedCodeFence = "R012";

    public static readonly IReadOnlyList<string> FixableRules = [DuplicateLines, EmptySections, UnclosedCodeFence];

    public static FixResult Fix(LintEngine engine, string text, RulesFormat format, IReadOnlyCollection<string> disabled)
    {
        var bom = text.StartsWith('﻿');
        var body = bom ? text[1..] : text;
        var newline = body.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var trailing = body.EndsWith('\n');
        var lines = body.Replace("\r\n", "\n").Split('\n').ToList();
        if (trailing)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var active = engine.Without(disabled);
        var applied = new List<Finding>();
        for (var pass = 0; pass < MaxPasses; pass++)
        {
            var findings = active.Analyze(string.Join('\n', lines), format).Findings
                .Where(f => FixableRules.Contains(f.RuleId))
                .ToList();
            if (findings.Count == 0)
            {
                break;
            }

            if (findings.FirstOrDefault(f => f.RuleId == UnclosedCodeFence) is { } fence)
            {
                lines.Add(ClosingFence(lines[fence.Line - 1]));
                applied.Add(fence);
                continue;
            }

            var delete = new SortedSet<int>();
            foreach (var finding in findings)
            {
                var index = finding.Line - 1;
                delete.Add(index);
                if (finding.RuleId == EmptySections)
                {
                    for (var next = index + 1; next < lines.Count && string.IsNullOrWhiteSpace(lines[next]); next++)
                    {
                        delete.Add(next);
                    }
                }

                applied.Add(finding);
            }

            foreach (var index in delete.Reverse())
            {
                lines.RemoveAt(index);
            }
        }

        var result = string.Join(newline, lines) + (trailing && lines.Count > 0 ? newline : "");
        return new FixResult(bom ? '﻿' + result : result, applied);
    }

    /// <summary>The opening fence's character, repeated as many times as it opens with.</summary>
    internal static string ClosingFence(string openingLine)
    {
        var trimmed = openingLine.TrimStart();
        var mark = trimmed[0];
        return new string(mark, trimmed.TakeWhile(c => c == mark).Count());
    }
}
