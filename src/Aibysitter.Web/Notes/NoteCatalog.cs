using System.Reflection;
using Aibysitter.Packs;
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
        All = EmbeddedFiles.Read(assembly, ResourcePrefix)
            .Where(f => f.Key.EndsWith(".md", StringComparison.Ordinal))
            .Select(f => Load(f.Key, f.Value))
            .OrderByDescending(n => n.Date)
            .ThenBy(n => n.Slug, StringComparer.Ordinal)
            .ToList();
    }

    public IReadOnlyList<Note> All { get; }

    public Note? Find(string slug) => All.FirstOrDefault(n => string.Equals(n.Slug, slug, StringComparison.OrdinalIgnoreCase));

    private static Note Load(string path, string text)
    {
        var resource = ResourcePrefix + path;
        var slug = path[..^".md".Length];
        var meta = ReadFrontmatter(text, resource);
        return new Note(
            slug,
            Required(meta, "title", resource),
            DateOnly.ParseExact(Required(meta, "date", resource), "yyyy-MM-dd"),
            Required(meta, "summary", resource),
            MarkdownRenderer.ToHtml(text));
    }

    private static Dictionary<string, string> ReadFrontmatter(string text, string resource) =>
        FrontmatterBlock.Parse(text)?.Values ?? throw new InvalidOperationException($"Note {resource} has no frontmatter block.");

    private static string Required(Dictionary<string, string> meta, string key, string resource) =>
        meta.TryGetValue(key, out var value) && value.Length > 0 ? value : throw new InvalidOperationException($"Note {resource} is missing \"{key}\".");
}
