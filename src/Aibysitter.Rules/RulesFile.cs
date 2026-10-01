using System.Text.RegularExpressions;

namespace Aibysitter.Rules;

public sealed partial class RulesFile
{
    private RulesFile(IReadOnlyList<RulesLine> lines, IReadOnlyList<Section> sections)
    {
        Lines = lines;
        Sections = sections;
    }

    public IReadOnlyList<RulesLine> Lines { get; }

    public IReadOnlyList<Section> Sections { get; }

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
        string? fenceMarker = null;
        var sectionHeading = string.Empty;
        var sectionLevel = 0;
        var sectionStart = 1;

        for (var i = 0; i < count; i++)
        {
            var number = i + 1;
            var lineText = raw[i];
            var fence = FenceRegex().Match(lineText);

            if (fence.Success && (!inFence || fence.Groups[1].Value.StartsWith(fenceMarker!, StringComparison.Ordinal)))
            {
                if (!inFence)
                {
                    fenceMarker = fence.Groups[1].Value[..3];
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

        return new RulesFile(lines, sections);
    }

    [GeneratedRegex(@"^\s{0,3}(`{3,}|~{3,})")]
    private static partial Regex FenceRegex();

    [GeneratedRegex(@"^\s{0,3}(#{1,6})\s+(.*?)\s*#*\s*$")]
    private static partial Regex HeadingRegex();
}
