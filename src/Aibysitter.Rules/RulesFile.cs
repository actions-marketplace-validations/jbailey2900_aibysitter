using System.Text.RegularExpressions;

namespace Aibysitter.Rules;

public sealed partial class RulesFile
{
    private RulesFile(IReadOnlyList<RulesLine> lines, IReadOnlyList<Section> sections, int? unclosedFenceLine)
    {
        Lines = lines;
        Sections = sections;
        UnclosedFenceLine = unclosedFenceLine;
        Suppressions = Suppressions.From(lines);
    }

    public IReadOnlyList<RulesLine> Lines { get; }

    public IReadOnlyList<Section> Sections { get; }

    public Suppressions Suppressions { get; }

    /// <summary>Line of a code fence that is still open at end of file; null when every fence closes.</summary>
    public int? UnclosedFenceLine { get; }

    public static RulesFile Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var raw = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var count = raw.Length;
        if (count > 0 && raw[count - 1].Length == 0)
        {
            count--;
        }

        var lines = new List<RulesLine>(count);
        var sections = new List<Section>();
        var inFence = false;
        var fenceOpenLine = 0;
        string? fenceMarker = null;
        var sectionHeading = string.Empty;
        var sectionLevel = 0;
        var sectionStart = 1;

        for (var i = 0; i < count; i++)
        {
            var number = i + 1;
            var lineText = raw[i];
            var fence = FenceRegex().Match(lineText);

            if (fence.Success && (!inFence || ClosesFence(fence, lineText, fenceMarker!)))
            {
                if (!inFence)
                {
                    fenceMarker = fence.Groups[1].Value;
                    fenceOpenLine = number;
                }

                inFence = !inFence;
                lines.Add(new RulesLine(number, lineText, IsHeading: false, IsInCodeFence: true));
                continue;
            }

            if (inFence)
            {
                lines.Add(new RulesLine(number, lineText, IsHeading: false, IsInCodeFence: true));
                continue;
            }

            var heading = HeadingRegex().Match(lineText);
            if (heading.Success)
            {
                if (number > sectionStart || sectionLevel > 0)
                {
                    sections.Add(new Section(sectionHeading, sectionLevel, sectionStart, number - 1));
                }

                sectionHeading = heading.Groups[2].Value.Trim();
                sectionLevel = heading.Groups[1].Value.Length;
                sectionStart = number;
                lines.Add(new RulesLine(number, lineText, IsHeading: true, IsInCodeFence: false));
                continue;
            }

            lines.Add(new RulesLine(number, lineText, IsHeading: false, IsInCodeFence: false));
        }

        if (count >= sectionStart)
        {
            sections.Add(new Section(sectionHeading, sectionLevel, sectionStart, count));
        }

        return new RulesFile(lines, sections, inFence ? fenceOpenLine : null);
    }

    /// <summary>A closing fence uses the opening character, is at least as long, and has nothing after it.</summary>
    private static bool ClosesFence(Match fence, string lineText, string opening) =>
        fence.Groups[1].Value[0] == opening[0]
        && fence.Groups[1].Value.Length >= opening.Length
        && string.IsNullOrWhiteSpace(lineText[(fence.Index + fence.Length)..]);

    [GeneratedRegex(@"^\s{0,3}(`{3,}|~{3,})")]
    private static partial Regex FenceRegex();

    [GeneratedRegex(@"^\s{0,3}(#{1,6})\s+(.*?)\s*#*\s*$")]
    private static partial Regex HeadingRegex();
}
