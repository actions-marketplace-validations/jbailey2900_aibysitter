using System.Text.RegularExpressions;

namespace Aibysitter.Rules.Rules;

/// <summary>
/// Repeated lines of four or more words. Skips horizontal rules, table rows, and suppression comments.
/// A heading counts as a repeat only under the same parent heading.
/// </summary>
public sealed partial class DuplicateLines : IRule
{
    public const int MinWords = 4;

    public string Id => "R005";
    public string Title => "Duplicate lines";
    public Severity Severity => Severity.Warning;

    public IEnumerable<Finding> Evaluate(RulesFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var firstSeen = new Dictionary<string, int>(StringComparer.Ordinal);
        var headingPath = new List<(int Level, string Key)>();

        foreach (var line in file.Lines.Where(l => !l.IsBlank && !l.IsInCodeFence && !l.IsFrontmatter && !l.IsDirective))
        {
            var key = TextNormalizer.Normalize(line.Text);

            if (line.IsHeading)
            {
                var level = HeadingLevelRegex().Match(line.Text).Groups[1].Value.Length;
                headingPath.RemoveAll(h => h.Level >= level);
                var parent = headingPath.Count == 0 ? string.Empty : headingPath[^1].Key;
                headingPath.Add((level, key));
                key = $"{parent}\u0000{key}";
            }

            if (HorizontalRuleRegex().IsMatch(line.Text) || InstructionText.IsTableRow(line.Text) || WordCount(line.Text) < MinWords)
            {
                continue;
            }

            if (firstSeen.TryGetValue(key, out var first))
            {
                yield return new Finding(Id, line.Number, $"Duplicate of line {first}.", "Delete the repeated line.");
            }
            else
            {
                firstSeen[key] = line.Number;
            }
        }
    }

    private static int WordCount(string text) => WordRegex().Count(TextNormalizer.Normalize(HeadingMarkRegex().Replace(text, string.Empty)));

    [GeneratedRegex(@"^\s{0,3}(?:(?:-\s*){3,}|(?:\*\s*){3,}|(?:_\s*){3,})$")]
    private static partial Regex HorizontalRuleRegex();

    [GeneratedRegex(@"^\s{0,3}(#{1,6})\s")]
    private static partial Regex HeadingLevelRegex();

    [GeneratedRegex(@"^\s{0,3}#{1,6}\s+")]
    private static partial Regex HeadingMarkRegex();

    [GeneratedRegex(@"[\p{L}\p{N}][\p{L}\p{N}'_.-]*")]
    private static partial Regex WordRegex();
}
