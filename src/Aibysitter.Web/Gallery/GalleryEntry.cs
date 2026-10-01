using Aibysitter.Rules;

namespace Aibysitter.Web.Gallery;

public sealed record GalleryFinding(Finding Finding, Severity Severity, string Title);

public sealed record GalleryEntry(
    string Id,
    string Name,
    string Description,
    string Category,
    IReadOnlyList<string> Tags,
    string FileName,
    string License,
    string Content,
    IReadOnlyList<GalleryFinding> Findings,
    LintScore Score)
{
    public IReadOnlyList<string> Lines { get; } = Content.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

    public string DownloadPath => $"/gallery/{Id}/{FileName}";
}
