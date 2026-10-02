using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aibysitter.Rules;

namespace Aibysitter.Packs;

/// <summary>
/// Packs embedded under <c>packs/&lt;id&gt;/</c>: <c>pack.json</c>, optional <c>intro.md</c>, and <c>NN-name.md</c> sections in
/// file order. Loading validates structure and throws <see cref="PackFormatException"/> with every problem found.
/// </summary>
public sealed partial class PackCatalog
{
    public const int SchemaVersion = 1;
    private const string Prefix = "packs/";

    public PackCatalog()
        : this(typeof(PackCatalog).Assembly)
    {
    }

    public PackCatalog(Assembly assembly)
        : this(EmbeddedFiles.Read(assembly, Prefix))
    {
    }

    /// <param name="files">Path relative to the packs root (<c>&lt;id&gt;/&lt;file&gt;</c>, <c>/</c> or <c>\</c>) to content.</param>
    public PackCatalog(IReadOnlyDictionary<string, string> files)
    {
        var errors = new List<string>();
        var folders = new SortedDictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var (key, text) in files.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            var path = EmbeddedFiles.Normalize(key);
            var slash = path.IndexOf('/');
            if (slash <= 0 || slash == path.Length - 1)
            {
                errors.Add($"{key}: path must be <id>/<file>");
                continue;
            }

            var folder = path[..slash];
            if (!folders.TryGetValue(folder, out var folderFiles))
            {
                folders[folder] = folderFiles = new Dictionary<string, string>(StringComparer.Ordinal);
            }

            if (!folderFiles.TryAdd(path[(slash + 1)..], text.Replace("\r\n", "\n")))
            {
                errors.Add($"{path}: appears more than once");
            }
        }

        var packs = new List<Pack>();
        foreach (var (folder, folderFiles) in folders)
        {
            var pack = Load(folder, folderFiles, errors);
            if (pack is not null)
            {
                packs.Add(pack);
            }
        }

        if (errors.Count > 0)
        {
            throw new PackFormatException(errors);
        }

        All = packs;
    }

    public IReadOnlyList<Pack> All { get; }

    public Pack? Find(string id) => All.FirstOrDefault(p => p.Id == id);

    public static IReadOnlyList<RulesFormat> OutputFormats { get; } =
        [RulesFormat.ClaudeMd, RulesFormat.AgentsMd, RulesFormat.GeminiMd, RulesFormat.CopilotInstructions, RulesFormat.CursorMdc, RulesFormat.CursorRules, RulesFormat.WindsurfRules];

    private static Pack? Load(string folder, Dictionary<string, string> files, List<string> errors)
    {
        var count = errors.Count;
        if (!files.TryGetValue("pack.json", out var json))
        {
            errors.Add($"{folder}: pack.json is missing");
            return null;
        }

        PackManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PackManifest>(json, new JsonSerializerOptions { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow });
        }
        catch (JsonException ex)
        {
            errors.Add($"{folder}/pack.json: {ex.Message}");
            return null;
        }

        if (manifest is null)
        {
            errors.Add($"{folder}/pack.json: empty");
            return null;
        }

        if (manifest.SchemaVersion != SchemaVersion) errors.Add($"{folder}/pack.json: schemaVersion must be {SchemaVersion}");
        if (manifest.Id != folder) errors.Add($"{folder}/pack.json: id \"{manifest.Id}\" must match the folder name");
        if (!IdRegex().IsMatch(manifest.Id ?? "")) errors.Add($"{folder}/pack.json: id must be lowercase kebab-case");
        if (string.IsNullOrWhiteSpace(manifest.Title)) errors.Add($"{folder}/pack.json: title is required");
        if (string.IsNullOrWhiteSpace(manifest.Description)) errors.Add($"{folder}/pack.json: description is required");
        if (manifest.Tags is not { Count: > 0 } || manifest.Tags.Any(string.IsNullOrWhiteSpace)) errors.Add($"{folder}/pack.json: tags must be non-empty strings");
        if (manifest.Targets is not { Count: > 0 }) errors.Add($"{folder}/pack.json: targets is required");
        foreach (var target in manifest.Targets ?? [])
        {
            if (!Enum.TryParse<RulesFormat>(target, out var format) || !OutputFormats.Contains(format) || format.ToString() != target)
            {
                errors.Add($"{folder}/pack.json: unknown target \"{target}\"");
            }
        }

        if (string.IsNullOrWhiteSpace(manifest.License)) errors.Add($"{folder}/pack.json: license is required");

        foreach (var (name, text) in files.Where(f => f.Key.EndsWith(".md", StringComparison.Ordinal)).OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            foreach (Match token in TokenRegex().Matches(text))
            {
                if (token.Value != PackComposer.RulesFileToken)
                {
                    errors.Add($"{folder}/{name}: unknown token {token.Value}");
                }
            }

            if (RulesFileNameRegex().Match(text) is { Success: true } named)
            {
                errors.Add($"{folder}/{name}: names a rules file ({named.Value}); write {PackComposer.RulesFileToken} so it matches the chosen format");
            }
        }

        var sections = new List<PackSection>();
        foreach (var (name, text) in files.Where(f => f.Key is not ("pack.json" or "intro.md")).OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            if (SectionFileRegex().Match(name) is not { Success: true } m)
            {
                errors.Add($"{folder}/{name}: section files are named NN-name.md");
                continue;
            }

            var markdown = text.Trim('\n', ' ');
            var lines = markdown.Split('\n');
            if (!lines[0].StartsWith("## ", StringComparison.Ordinal) || lines[0].Length <= 3)
            {
                errors.Add($"{folder}/{name}: must start with one H2 heading");
                continue;
            }

            if (OutsideFences(lines.Skip(1)).Any(l => HeadingRegex().IsMatch(l)))
            {
                errors.Add($"{folder}/{name}: only one H1 or H2 heading is allowed");
            }

            if (lines.Skip(1).All(string.IsNullOrWhiteSpace))
            {
                errors.Add($"{folder}/{name}: heading has no content");
            }

            sections.Add(new PackSection(m.Groups["id"].Value, lines[0][3..].Trim(), markdown));
        }

        if (sections.Count == 0) errors.Add($"{folder}: no sections");
        foreach (var dup in sections.GroupBy(s => s.Heading, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            errors.Add($"{folder}: heading \"{dup.Key}\" appears more than once");
        }

        string? intro = null;
        if (files.TryGetValue("intro.md", out var introText))
        {
            intro = introText.Trim('\n', ' ');
            if (intro.Length == 0 || OutsideFences(intro.Split('\n')).Any(l => l.StartsWith('#')))
            {
                errors.Add($"{folder}/intro.md: must be non-empty text without headings");
            }
        }

        return errors.Count == count ? new Pack(manifest, intro, sections) : null;
    }

    /// <summary>Lines not inside a ``` or ~~~ fenced block.</summary>
    private static IEnumerable<string> OutsideFences(IEnumerable<string> lines)
    {
        string? fence = null;
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (fence is null && (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal)))
            {
                fence = trimmed[..3];
                continue;
            }

            if (fence is not null)
            {
                if (trimmed.StartsWith(fence, StringComparison.Ordinal))
                {
                    fence = null;
                }

                continue;
            }

            yield return line;
        }
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex IdRegex();

    [GeneratedRegex(@"^(?<order>\d{2})-(?<id>[a-z0-9]+(?:-[a-z0-9]+)*)\.md$")]
    private static partial Regex SectionFileRegex();

    [GeneratedRegex(@"\{\{[^}]*\}\}")]
    private static partial Regex TokenRegex();

    /// <summary>Rules file names that depend on the output format.</summary>
    [GeneratedRegex(@"(?<![\w.-])(?:CLAUDE\.md|AGENTS\.md|GEMINI\.md|copilot-instructions\.md|\.cursorrules|\.windsurfrules|[\w-]+\.mdc)(?![\w-]|\.\w)")]
    private static partial Regex RulesFileNameRegex();

    [GeneratedRegex("^#{1,2} ")]
    private static partial Regex HeadingRegex();
}

public sealed class PackFormatException(IReadOnlyList<string> errors)
    : Exception("Invalid packs:\n" + string.Join("\n", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
