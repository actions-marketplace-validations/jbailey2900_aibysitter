using System.Text.RegularExpressions;

namespace Aibysitter.Rules;

public enum RulesFormat
{
    /// <summary>Resolve from content: <see cref="CursorMdc"/> when frontmatter has Cursor keys, otherwise <see cref="Markdown"/>.</summary>
    Auto,

    /// <summary>Markdown rules file with no format-specific checks.</summary>
    Markdown,
    ClaudeMd,
    AgentsMd,
    CursorMdc,
    CursorRules,
    CopilotInstructions,
    GeminiMd,
    WindsurfRules,
}

public static partial class RulesFormats
{
    /// <summary>Frontmatter keys Cursor reads from <c>.cursor/rules/*.mdc</c>.</summary>
    public static readonly IReadOnlySet<string> CursorKeys = new HashSet<string>(StringComparer.Ordinal) { "description", "globs", "alwaysApply" };

    /// <summary>Formats selectable on the lint page, in display order.</summary>
    public static IReadOnlyList<RulesFormat> Selectable { get; } = Enum.GetValues<RulesFormat>().Where(f => f != RulesFormat.Markdown).ToList();

    public static string DisplayName(RulesFormat format) => format switch
    {
        RulesFormat.Auto => "Auto-detect",
        RulesFormat.Markdown => "Markdown rules file",
        RulesFormat.ClaudeMd => "CLAUDE.md",
        RulesFormat.AgentsMd => "AGENTS.md",
        RulesFormat.CursorMdc => "Cursor rule (.mdc)",
        RulesFormat.CursorRules => ".cursorrules",
        RulesFormat.CopilotInstructions => "Copilot instructions",
        RulesFormat.GeminiMd => "GEMINI.md",
        RulesFormat.WindsurfRules => ".windsurfrules",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>Where the file goes in a repository. <paramref name="fileName"/> is used for .mdc rules.</summary>
    public static string InstallPath(RulesFormat format, string fileName) => format switch
    {
        RulesFormat.CursorMdc => $".cursor/rules/{fileName}",
        RulesFormat.CopilotInstructions => ".github/copilot-instructions.md",
        _ => fileName,
    };

    /// <summary>Format from a file name or path; null when the name is not a known rules file.</summary>
    public static RulesFormat? FromFileName(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var name = path.Replace('\\', '/')[(path.Replace('\\', '/').LastIndexOf('/') + 1)..];

        return name switch
        {
            "CLAUDE.md" => RulesFormat.ClaudeMd,
            "AGENTS.md" => RulesFormat.AgentsMd,
            ".cursorrules" => RulesFormat.CursorRules,
            "copilot-instructions.md" => RulesFormat.CopilotInstructions,
            "GEMINI.md" => RulesFormat.GeminiMd,
            ".windsurfrules" => RulesFormat.WindsurfRules,
            _ when MdcRegex().IsMatch(name) => RulesFormat.CursorMdc,
            _ => null,
        };
    }

    [GeneratedRegex(@"^[A-Za-z0-9][\w.-]*\.mdc$")]
    private static partial Regex MdcRegex();
}
