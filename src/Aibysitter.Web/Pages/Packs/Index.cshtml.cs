using Aibysitter.Web.RulesPacks;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aibysitter.Web.Pages.Packs;

public class IndexModel(PackScores scores) : PageModel
{
    public IReadOnlyList<PackView> Packs => scores.All;

    public void OnGet()
    {
    }
}
