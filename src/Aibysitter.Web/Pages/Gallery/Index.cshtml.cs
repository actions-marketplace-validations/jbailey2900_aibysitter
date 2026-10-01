using Aibysitter.Web.Gallery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aibysitter.Web.Pages.Gallery;

public class IndexModel(GalleryCatalog catalog) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Tag { get; set; }

    public IReadOnlyList<string> Categories => catalog.Categories;

    public IReadOnlyList<GalleryEntry> Entries { get; private set; } = [];

    public bool IsUnfiltered => Category is null && Tag is null;

    public void OnGet()
    {
        Entries = catalog.All
            .Where(e => Category is null || string.Equals(e.Category, Category, StringComparison.OrdinalIgnoreCase))
            .Where(e => Tag is null || e.Tags.Contains(Tag, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }
}
