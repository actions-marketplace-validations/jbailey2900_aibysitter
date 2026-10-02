using Aibysitter.Web.Notes;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aibysitter.Web.Pages.Notes;

public class IndexModel(NoteCatalog catalog) : PageModel
{
    public IReadOnlyList<Note> Notes => catalog.All;

    public void OnGet()
    {
    }
}
