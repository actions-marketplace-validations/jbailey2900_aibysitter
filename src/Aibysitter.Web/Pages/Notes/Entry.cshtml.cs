using System.Text.RegularExpressions;
using Aibysitter.Web.Notes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aibysitter.Web.Pages.Notes;

/// <summary>A field note. Rule and check IDs (old /Notes/{id} rule URLs) redirect permanently to /Rules/{id}.</summary>
public partial class EntryModel(NoteCatalog catalog) : PageModel
{
    public Note Note { get; private set; } = null!;

    public IActionResult OnGet(string slug)
    {
        if (RuleIdRegex().IsMatch(slug))
        {
            return RedirectPermanent($"/Rules/{slug.ToUpperInvariant()}");
        }

        if (catalog.Find(slug) is not { } note)
        {
            return NotFound();
        }

        Note = note;
        return Page();
    }

    [GeneratedRegex(@"^[PR]\d{3}$", RegexOptions.IgnoreCase)]
    private static partial Regex RuleIdRegex();
}
