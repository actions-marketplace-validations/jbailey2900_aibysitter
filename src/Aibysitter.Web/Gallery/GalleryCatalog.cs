using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aibysitter.Packs;
using Aibysitter.Rules;

namespace Aibysitter.Web.Gallery;

/// <summary>
/// Rules-file gallery built from embedded resources under <c>gallery/&lt;id&gt;/</c> (entry.json + the rules file).
/// Loaded and linted once per process.
/// </summary>
public sealed partial class GalleryCatalog
{
    public const string ResourcePrefix = "gallery/";

    private readonly Lazy<IReadOnlyList<GalleryEntry>> entries;

    public GalleryCatalog(LintEngine engine)
        : this(engine, typeof(GalleryCatalog).Assembly)
    {
    }

    public GalleryCatalog(LintEngine engine, Assembly assembly)
    {
        entries = new Lazy<IReadOnlyList<GalleryEntry>>(() => Load(engine, assembly));
    }

    public IReadOnlyList<GalleryEntry> All => entries.Value;

    public GalleryEntry? Find(string id) => All.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<string> Categories => All.Select(e => e.Category).Distinct(StringComparer.Ordinal).Order(StringComparer.OrdinalIgnoreCase).ToList();

    public IReadOnlyList<string> Tags => All.SelectMany(e => e.Tags).Distinct(StringComparer.Ordinal).Order(StringComparer.OrdinalIgnoreCase).ToList();

    private static IReadOnlyList<GalleryEntry> Load(LintEngine engine, Assembly assembly)
    {
        var files = EmbeddedFiles.Read(assembly, ResourcePrefix);

        var severities = engine.Rules.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var list = new List<GalleryEntry>();

        foreach (var folder in files.Keys.Select(p => p.Split('/')[0]).Distinct(StringComparer.Ordinal))
        {
            if (!files.TryGetValue($"{folder}/entry.json", out var manifestJson))
            {
                throw new InvalidOperationException($"Gallery entry '{folder}' has no entry.json.");
            }

            var manifest = JsonSerializer.Deserialize<Manifest>(manifestJson, JsonOptions)
                ?? throw new InvalidOperationException($"Gallery entry '{folder}': entry.json is empty.");
            Validate(folder, manifest);

            if (!files.TryGetValue($"{folder}/{manifest.File}", out var content))
            {
                throw new InvalidOperationException($"Gallery entry '{folder}': file '{manifest.File}' not found.");
            }

            var format = RulesFormats.FromFileName(manifest.Path ?? manifest.File!)!.Value;
            var findings = engine.Lint(content, format);
            list.Add(new GalleryEntry(
                manifest.Id!,
                manifest.Name!,
                manifest.Description!,
                manifest.Category!,
                manifest.Tags!,
                manifest.File!,
                manifest.License!,
                content,
                findings.Select(f => new GalleryFinding(f, severities[f.RuleId].Severity, severities[f.RuleId].Title)).ToList(),
                engine.Score(findings),
                format,
                manifest.Path));
        }

        return list.OrderBy(e => e.Category, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void Validate(string folder, Manifest m)
    {
        void Require(bool ok, string message)
        {
            if (!ok)
            {
                throw new InvalidOperationException($"Gallery entry '{folder}': {message}");
            }
        }

        Require(m.Id == folder, $"id '{m.Id}' must match its folder name");
        Require(IdRegex().IsMatch(m.Id ?? string.Empty), "id must be lowercase letters, digits and hyphens");
        Require(!string.IsNullOrWhiteSpace(m.Name), "name is required");
        Require(!string.IsNullOrWhiteSpace(m.Description), "description is required");
        Require(!string.IsNullOrWhiteSpace(m.Category), "category is required");
        Require(m.Tags is { Count: > 0 } && m.Tags.All(t => !string.IsNullOrWhiteSpace(t)), "at least one non-empty tag is required");
        Require(m.File is not null && RulesFormats.FromFileName(m.Path ?? m.File) is not null, "file (or path) must be a known rules file (CLAUDE.md, AGENTS.md, .cursor/rules/*.mdc, .cursorrules, copilot-instructions.md, GEMINI.md, .windsurfrules)");
        Require(m.Path is null || m.Path.Replace('\\', '/').EndsWith("/" + m.File, StringComparison.Ordinal), "path must end with the file name");
        Require(!string.IsNullOrWhiteSpace(m.License), "license is required");
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <param name="Path">Repository path when the file name alone does not identify the format (.mdc under .cursor/rules/).</param>
    private sealed record Manifest(string? Id, string? Name, string? Description, string? Category, List<string>? Tags, string? File, string? License, string? Path = null);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex IdRegex();
}
