using Aibysitter.Rules;
using Aibysitter.Rules.PullRequests;
using Aibysitter.Web.Gallery;
using Aibysitter.Web.Notes;

namespace Aibysitter.Web.Seo;

/// <param name="Path">Canonical root-relative path.</param>
/// <param name="LastModified">Set for notes only.</param>
public sealed record SiteMapEntry(string Path, DateOnly? LastModified = null);

/// <summary>Every indexable page: fixed pages, rule and check pages, rendered gallery entries, notes.</summary>
public sealed class SiteMap(GalleryCatalog gallery, NoteCatalog notes)
{
    public static readonly IReadOnlyList<string> FixedPages =
    [
        "/", "/Lint", "/Gallery", "/Packs", "/Rules", "/Rules/Methodology", "/Rules/Changelog",
        "/GitHub", "/GitHub/Config", "/API", "/Notes", "/About", "/Privacy",
    ];

    public IReadOnlyList<SiteMapEntry> Entries =>
    [
        .. FixedPages.Select(p => new SiteMapEntry(p)),
        .. RuleDocs.All.Concat(PullRequestCheckDocs.All).Select(d => new SiteMapEntry(RulePath(d.Id))),
        .. gallery.All.Select(e => new SiteMapEntry(GalleryPath(e.Id))),
        .. notes.All.Select(n => new SiteMapEntry(NotePath(n.Slug), n.Date)),
    ];

    public static string RulePath(string id) => $"/Rules/{id}";

    public static string GalleryPath(string id) => $"/Gallery/{id}";

    public static string NotePath(string slug) => $"/Notes/{slug}";
}
