using System.Text.RegularExpressions;

namespace Aibysitter.Rules.Rules;

/// <summary>
/// A heading with no content before the next heading of the same or higher level, or before end of file.
/// The file's first heading is exempt when it is level 1 (document title).
/// </summary>
public sealed partial class EmptySections : IRule
{
    public string Id => "R011";
    public string Title => "Empty sections";
    public Severity Severity => Severity.Warning;

    public IEnumerable<Finding> Evaluate(RulesFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var lines = file.Lines;
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].IsHeading)
            {
                continue;
            }

            var level = Level(lines[i].Text);
            if (level == 1 && !lines.Take(i).Any(l => l.IsHeading))
            {
                continue;
            }

            var empty = true;
            for (var j = i + 1; j < lines.Count; j++)
            {
                if (lines[j].IsHeading)
                {
                    empty = Level(lines[j].Text) <= level;
                    break;
                }

                if (!lines[j].IsBlank && !lines[j].IsDirective && !HtmlCommentRegex().IsMatch(lines[j].Text))
                {
                    empty = false;
                    break;
                }
            }

            if (empty)
            {
                yield return new Finding(
                    Id,
                    lines[i].Number,
                    $"Section \"{HeadingTextRegex().Replace(lines[i].Text, string.Empty).Trim()}\" has no content.",
                    "Add the rules for this section, or delete the heading.");
            }
        }
    }

    private static int Level(string text) => LevelRegex().Match(text).Groups[1].Value.Length;

    [GeneratedRegex(@"^\s{0,3}(#{1,6})\s")]
    private static partial Regex LevelRegex();

    [GeneratedRegex(@"^\s{0,3}#{1,6}\s+|\s+#+\s*$")]
    private static partial Regex HeadingTextRegex();

    [GeneratedRegex(@"^\s*<!--.*-->\s*$")]
    private static partial Regex HtmlCommentRegex();
}
