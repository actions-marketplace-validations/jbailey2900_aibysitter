using System.Reflection;
using Aibysitter.Web.Content;

namespace Aibysitter.Web.Notes;

public sealed record Note(string Slug, string Title, DateOnly Date, string Summary, string Html);

/// <summary>
/// Field notes embedded from Notes/Content/{slug}.md. Each file starts with a frontmatter block holding
/// title, date (yyyy-MM-dd) and summary. Newest first.
/// </summary>
public sealed class NoteCatalog
{
    public const string ResourcePrefix = "notes/";

    public NoteCatalog()
        : this(typeof(NoteCatalog).Assembly)
    {
    }

    internal NoteCatalog(Assembly assembly)
    {
        All = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".md", StringComparison.Ordinal))
            .Select(n => Load(assembly, n))
            .OrderByDescending(n => n.Date)
            .ThenBy(n => n.Slug, StringComparer.Ordinal)
            .ToList();
    }

    public IReadOnlyList<Note> All { get; }

    public Note? Find(string slug) => All.FirstOrDefault(n => string.Equals(n.Slug, slug, StringComparison.OrdinalIgnoreCase));

    private static Note Load(Assembly assembly, string resource)
    {
        using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
        var text = reader.ReadToEnd();
        var slug = resource[ResourcePrefix.Length..^".md".Length];
        var meta = ReadFrontmatter(text, resource);
        return new Note(
            slug,
            Required(meta, "title", resource),
            DateOnly.ParseExact(Required(meta, "date", resource), "yyyy-MM-dd"),
            Required(meta, "summary", resource),
            MarkdownRenderer.ToHtml(text));
    }

    private static Dictionary<string, string> ReadFrontmatter(string text, string resource)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var end = Array.IndexOf(lines, "---", 1);
        if (lines.Length == 0 || lines[0] != "---" || end < 0)
        {
            throw new InvalidOperationException($"Note {resource} has no frontmatter block.");
        }

        return lines[1..end]
            .Select(l => l.Split(':', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.Ordinal);
    }

    private static string Required(Dictionary<string, string> meta, string key, string resource) =>
        meta.TryGetValue(key, out var value) && value.Length > 0 ? value : throw new InvalidOperationException($"Note {resource} is missing \"{key}\".");
}
