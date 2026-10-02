using System.Text;
using System.Text.RegularExpressions;
using Aibysitter.Rules;

namespace Aibysitter.Packs;

/// <summary>
/// Composes packs into one rules file: optional frontmatter (.mdc), <c># title</c>, intro when exactly one pack is chosen,
/// then sections in pack order. A heading that appears in more than one pack becomes one section at its first position,
/// with later packs' lines appended and repeated list items dropped. LF line endings, trailing newline.
/// </summary>
public static partial class PackComposer
{
    public const string DefaultTitle = "Project rules";

    /// <summary>Short names for <c>--format</c>; full <see cref="RulesFormat"/> names are accepted too.</summary>
    public static readonly IReadOnlyDictionary<string, RulesFormat> ShortNames = new Dictionary<string, RulesFormat>(StringComparer.OrdinalIgnoreCase)
    {
        ["claude"] = RulesFormat.ClaudeMd,
        ["agents"] = RulesFormat.AgentsMd,
        ["gemini"] = RulesFormat.GeminiMd,
        ["copilot"] = RulesFormat.CopilotInstructions,
        ["cursor"] = RulesFormat.CursorMdc,
        ["cursorrules"] = RulesFormat.CursorRules,
        ["windsurf"] = RulesFormat.WindsurfRules,
    };

    public static bool TryParseFormat(string name, out RulesFormat format)
    {
        if (ShortNames.TryGetValue(name, out format))
        {
            return true;
        }

        return RulesFormats.TryParse(name, out format) && PackCatalog.OutputFormats.Contains(format);
    }

    /// <summary>Where the composed file goes, relative to the repository root.</summary>
    public static string DefaultPath(RulesFormat format, IReadOnlyList<Pack> packs) => format switch
    {
        RulesFormat.ClaudeMd => "CLAUDE.md",
        RulesFormat.AgentsMd => "AGENTS.md",
        RulesFormat.GeminiMd => "GEMINI.md",
        RulesFormat.CopilotInstructions => RulesFormats.InstallPath(format, "copilot-instructions.md"),
        RulesFormat.CursorMdc => RulesFormats.InstallPath(format, (packs.Count == 1 ? packs[0].Id : "project-rules") + ".mdc"),
        RulesFormat.CursorRules => ".cursorrules",
        RulesFormat.WindsurfRules => ".windsurfrules",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Not a pack output format."),
    };

    public static string Compose(IReadOnlyList<Pack> packs, RulesFormat format, string? title = null)
    {
        ArgumentOutOfRangeException.ThrowIfZero(packs.Count);
        if (!PackCatalog.OutputFormats.Contains(format))
        {
            throw new ArgumentOutOfRangeException(nameof(format), format, "Not a pack output format.");
        }

        var heading = string.IsNullOrWhiteSpace(title) ? DefaultTitle : title.Trim();
        var text = new StringBuilder();
        if (format == RulesFormat.CursorMdc)
        {
            text.Append("---\n").Append("description: ").Append(heading).Append('\n').Append("alwaysApply: true\n").Append("---\n\n");
        }

        text.Append("# ").Append(heading).Append('\n');
        if (packs.Count == 1 && packs[0].Intro is { } intro)
        {
            text.Append('\n').Append(intro).Append('\n');
        }

        foreach (var (sectionHeading, lines) in Merge(packs))
        {
            text.Append('\n').Append("## ").Append(sectionHeading).Append('\n');
            foreach (var line in lines)
            {
                text.Append(line).Append('\n');
            }
        }

        return text.ToString();
    }

    private static List<(string Heading, List<string> Lines)> Merge(IReadOnlyList<Pack> packs)
    {
        var merged = new List<(string Heading, List<string> Lines)>();
        foreach (var section in packs.SelectMany(p => p.Sections))
        {
            var existing = merged.FindIndex(m => string.Equals(m.Heading, section.Heading, StringComparison.OrdinalIgnoreCase));
            if (existing < 0)
            {
                merged.Add((section.Heading, [.. section.BodyLines]));
                continue;
            }

            var lines = merged[existing].Lines;
            var seen = lines.Where(IsListItem).Select(l => l.Trim()).ToHashSet(StringComparer.Ordinal);
            var added = section.BodyLines.Where(l => !(IsListItem(l) && seen.Contains(l.Trim()))).ToList();
            if (added.Count == 0)
            {
                continue;
            }

            if (!(IsListItem(lines[^1]) && IsListItem(added[0])))
            {
                lines.Add(string.Empty);
            }

            lines.AddRange(added);
        }

        return merged;
    }

    private static bool IsListItem(string line) => ListItemRegex().IsMatch(line);

    [GeneratedRegex(@"^\s*(?:[-*+]|\d+[.)])\s")]
    private static partial Regex ListItemRegex();
}
