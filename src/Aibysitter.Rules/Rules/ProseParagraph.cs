using System.Text.RegularExpressions;

namespace Aibysitter.Rules.Rules;

/// <summary>
/// Paragraphs (consecutive prose lines that are not list items, list-item continuation lines, table rows, or
/// block quotes) longer than <see cref="MaxWords"/> words that contain at least one instruction sentence
/// (<see cref="InstructionText.IsInstruction"/>). Reported at the paragraph's first line.
/// </summary>
public sealed partial class ProseParagraph : IRule
{
    public const int MaxWords = 80;

    public string Id => "R013";
    public string Title => "Prose paragraph";
    public Severity Severity => Severity.Info;

    public IEnumerable<Finding> Evaluate(RulesFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var start = 0;
        var words = 0;
        var text = new System.Text.StringBuilder();
        var inList = false;
        foreach (var line in file.Lines.Append(null))
        {
            if (line is not null && NonParagraphRegex().IsMatch(line.Text) && line.IsProse)
            {
                inList = true;
            }
            else if (line is null || line.IsBlank || line.IsHeading || line.IsInCodeFence)
            {
                inList = false;
            }

            if (line is not null && !inList && IsParagraphLine(line))
            {
                if (words == 0)
                {
                    start = line.Number;
                }

                words += WordRegex().Count(InstructionText.WithoutCode(line.Text));
                text.Append(line.Text.Trim()).Append(' ');
                continue;
            }

            if (words > MaxWords && HasInstruction(text.ToString()))
            {
                yield return new Finding(
                    Id,
                    start,
                    $"Paragraph of {words} words; limit is {MaxWords}.",
                    "Split it into list items, one instruction each.");
            }

            words = 0;
            text.Clear();
        }
    }

    private static bool HasInstruction(string paragraph) =>
        InstructionText.Sentences(paragraph).Any(InstructionText.IsInstruction);

    private static bool IsParagraphLine(RulesLine line) =>
        line.IsProse && !InstructionText.IsTableRow(line.Text) && !NonParagraphRegex().IsMatch(line.Text);

    /// <summary>List items, block quotes, HTML lines.</summary>
    [GeneratedRegex(@"^\s*(?:[-*+]\s|\d+[.)]\s|>|<)")]
    private static partial Regex NonParagraphRegex();

    [GeneratedRegex(@"[\p{L}\p{N}][\p{L}\p{N}'_.-]*")]
    private static partial Regex WordRegex();
}
