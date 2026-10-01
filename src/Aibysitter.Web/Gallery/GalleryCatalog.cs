using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aibysitter.Rules;

namespace Aibysitter.Web.Gallery;

/// <summary>
/// Rules-file gallery built from embedded resources under <c>gallery/&lt;id&gt;/</c> (entry.json + the rules file).
/// Loaded and linted once per process.
/// </summary>
public sealed partial class GalleryCatalog
{
    public const string ResourcePrefix = "gallery/";
    public static readonly IReadOnlySet<string> AllowedFileNames = new HashSet<string>(StringComparer.Ordinal) { "CLAUDE.md", "AGENTS.md" };

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
        var resources = assembly.GetManifestResourceNames()
            .Select(raw => (Raw: raw, Path: raw.Replace('\\', '/')))
            .Where(r => r.Path.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .ToDictionary(r => r.Path, r => r.Raw, StringComparer.Ordinal);

        var severities = engine.Rules.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var list = new List<GalleryEntry>();

        foreach (var folder in resources.Keys.Select(p => p.Split('/')[1]).Distinct(StringComparer.Ordinal))
        {
            var manifestPath = $"{ResourcePrefix}{folder}/entry.json";
            if (!resources.TryGetValue(manifestPath, out var manifestResource))
            {
                throw new InvalidOperationException($"Gallery entry '{folder}' has no entry.json.");
            }

            var manifest = JsonSerializer.Deserialize<Manifest>(Read(assembly, manifestResource), JsonOptions)
                ?? throw new InvalidOperationException($"Gallery entry '{folder}': entry.json is empty.");
            Validate(folder, manifest);

            if (!resources.TryGetValue($"{ResourcePrefix}{folder}/{manifest.File}", out var fileResource))
            {
                throw new InvalidOperationException($"Gallery entry '{folder}': file '{manifest.File}' not found.");
            }

            var content = Read(assembly, fileResource);
            var findings = engine.Lint(content);
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
                engine.Score(findings)));
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
        Require(m.File is not null && AllowedFileNames.Contains(m.File), "file must be CLAUDE.md or AGENTS.md");
        Require(!string.IsNullOrWhiteSpace(m.License), "license is required");
    }

    private static string Read(Assembly assembly, string resource)
    {
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record Manifest(string? Id, string? Name, string? Description, string? Category, List<string>? Tags, string? File, string? License);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex IdRegex();
}
