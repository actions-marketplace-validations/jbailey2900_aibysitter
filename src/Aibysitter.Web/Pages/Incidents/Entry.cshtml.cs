using Aibysitter.Web.Incidents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aibysitter.Web.Pages.Incidents;

public class EntryModel(IncidentCatalog catalog) : PageModel
{
    public Incident Incident { get; private set; } = null!;

    public IActionResult OnGet(int id)
    {
        if (catalog.Find(id) is not { } incident)
        {
            return NotFound();
        }

        Incident = incident;
        return Page();
    }
}
