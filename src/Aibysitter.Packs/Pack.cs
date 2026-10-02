using System.Text.Json.Serialization;

namespace Aibysitter.Packs;

/// <summary><c>pack.json</c>, schema version 1.</summary>
public sealed record PackManifest(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("targets")] IReadOnlyList<string> Targets,
    [property: JsonPropertyName("license")] string License,
    [property: JsonPropertyName("source")] string? Source = null);

/// <param name="Id">File name without the order prefix and extension.</param>
/// <param name="Heading">Text of the section's H2.</param>
/// <param name="Markdown">The section file: the H2 line and its body, no trailing newline.</param>
public sealed record PackSection(string Id, string Heading, string Markdown)
{
    /// <summary>Lines after the heading, without leading or trailing blank lines.</summary>
    public IReadOnlyList<string> BodyLines { get; } = Markdown.Split('\n').Skip(1).SkipWhile(string.IsNullOrWhiteSpace).Reverse().SkipWhile(string.IsNullOrWhiteSpace).Reverse().ToList();
}

/// <param name="Intro">Optional <c>intro.md</c>, written only when this pack is composed alone.</param>
public sealed record Pack(PackManifest Manifest, string? Intro, IReadOnlyList<PackSection> Sections)
{
    public string Id => Manifest.Id;
}
