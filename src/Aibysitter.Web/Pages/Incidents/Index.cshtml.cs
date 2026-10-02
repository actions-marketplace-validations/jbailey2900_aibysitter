using Aibysitter.Web.Incidents;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aibysitter.Web.Pages.Incidents;

public class IndexModel(IncidentCatalog catalog) : PageModel
{
    public IReadOnlyList<Incident> Incidents => catalog.All;

    public int Caught => Incidents.Count(i => i.CaughtBy.Count > 0);

    public void OnGet()
    {
    }
}
