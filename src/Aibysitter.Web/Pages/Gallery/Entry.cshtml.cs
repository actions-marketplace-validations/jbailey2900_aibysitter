using Aibysitter.Web.Gallery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aibysitter.Web.Pages.Gallery;

public class EntryModel(GalleryCatalog catalog) : PageModel
{
    public GalleryEntry Entry { get; private set; } = null!;

    public string BadgeMarkdown { get; private set; } = string.Empty;

    public IReadOnlyDictionary<int, List<GalleryFinding>> FindingsByLine { get; private set; } = new Dictionary<int, List<GalleryFinding>>();

    public IActionResult OnGet(string id)
    {
        if (catalog.Find(id) is not { } entry)
        {
            return NotFound();
        }

        Entry = entry;
        var origin = $"{Request.Scheme}://{Request.Host}";
        BadgeMarkdown = $"[![aibysitter {entry.Score.Grade} {entry.Score.Value}]({origin}{entry.BadgePath})]({origin}/Gallery/{entry.Id})";
        FindingsByLine = entry.Findings.GroupBy(f => f.Finding.Line).ToDictionary(g => g.Key, g => g.ToList());
        return Page();
    }
}
