using System.Text.RegularExpressions;

namespace Aibysitter.Rules.Rules;

/// <summary>
/// Repeated instruction lines of four or more words: lines an instruction sentence overlaps
/// (<see cref="InstructionText.InstructionLines"/>). Not counted: headings, horizontal rules, table rows, HTML comments,
/// suppression comments, wrapped continuation lines (lowercase start after an unfinished prose line), indented code
/// (four spaces or a tab after a blank line, not a list item). Not reported:
/// lines that occur <see cref="TemplateCount"/> or more times (template lines), and repeats whose previous or next line
/// also repeats the line beside the first occurrence (repeated blocks such as parallel procedures).
/// </summary>
public sealed partial class DuplicateLines : IRule
{
    public const int MinWords = 4;

    /// <summary>Occurrences at which a repeated line is read as a template line.</summary>
    public const int TemplateCount = 3;

    public string Id => "R005";
    public string Title => "Duplicate lines";
    public Severity Severity => Severity.Warning;

    public IEnumerable<Finding> Evaluate(RulesFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var lines = file.Lines;
        var keys = lines.Select(l => l.IsBlank ? null : TextNormalizer.Normalize(l.Text)).ToList();
        var instructionLines = InstructionText.InstructionLines(file);
        var candidates = Enumerable.Range(0, lines.Count).Where(i => IsCandidate(lines, i, instructionLines)).ToList();
        var counts = candidates.GroupBy(i => keys[i]!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var firstSeen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var i in candidates)
        {
            var key = keys[i]!;
            if (!firstSeen.TryGetValue(key, out var first))
            {
                firstSeen[key] = i;
                continue;
            }

            if (counts[key] >= TemplateCount || SameNeighbour(keys, first, i, -1) || SameNeighbour(keys, first, i, 1))
            {
                continue;
            }

            yield return new Finding(Id, lines[i].Number, $"Duplicate of line {lines[first].Number}.", "Delete the repeated line.");
        }
    }

    private static bool IsCandidate(IReadOnlyList<RulesLine> lines, int i, IReadOnlySet<int> instructionLines)
    {
        var line = lines[i];
        return instructionLines.Contains(line.Number)
            && !InstructionText.IsHtmlComment(line.Text)
            && !HorizontalRuleRegex().IsMatch(line.Text)
            && WordCount(line.Text) >= MinWords
            && !IsContinuation(lines, i)
            && !IsIndentedCode(lines, i);
    }

    /// <summary>Indented four or more spaces (or a tab) after a blank line, and not a list item: an indented code block.</summary>
    private static bool IsIndentedCode(IReadOnlyList<RulesLine> lines, int i) =>
        i > 0 && lines[i - 1].IsBlank && IndentedRegex().IsMatch(lines[i].Text) && !InstructionText.IsListItem(lines[i].Text);

    /// <summary>Lowercase start, not a list item, after a prose line that does not end a sentence or introduce a list.</summary>
    private static bool IsContinuation(IReadOnlyList<RulesLine> lines, int i) =>
        i > 0
        && lines[i - 1].IsProse
        && !InstructionText.IsListItem(lines[i].Text)
        && LowercaseStartRegex().IsMatch(lines[i].Text)
        && !SentenceEndRegex().IsMatch(lines[i - 1].Text);

    private static bool SameNeighbour(IReadOnlyList<string?> keys, int first, int repeat, int offset)
    {
        int a = first + offset, b = repeat + offset;
        return a >= 0 && b >= 0 && a < keys.Count && b < keys.Count && b != first && keys[a] is not null && keys[a] == keys[b];
    }

    private static int WordCount(string text) => WordRegex().Count(TextNormalizer.Normalize(text));

    [GeneratedRegex(@"^\s{0,3}(?:(?:-\s*){3,}|(?:\*\s*){3,}|(?:_\s*){3,})$")]
    private static partial Regex HorizontalRuleRegex();

    [GeneratedRegex(@"^(?: {4}|\t)")]
    private static partial Regex IndentedRegex();

    [GeneratedRegex(@"^\s*[a-z]")]
    private static partial Regex LowercaseStartRegex();

    [GeneratedRegex(@"[.!?:]\s*$")]
    private static partial Regex SentenceEndRegex();

    [GeneratedRegex(@"[\p{L}\p{N}][\p{L}\p{N}'_.-]*")]
    private static partial Regex WordRegex();
}
