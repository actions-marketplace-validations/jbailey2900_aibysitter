using Aibysitter.Rules;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aibysitter.Web.Pages.Notes;

public class RuleModel(LintEngine engine) : PageModel
{
    public RuleDoc Doc { get; private set; } = null!;

    public Severity Severity { get; private set; }

    public IActionResult OnGet(string id)
    {
        var doc = RuleDocs.Find(id);
        var rule = engine.Rules.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
        if (doc is null || rule is null)
        {
            return NotFound();
        }

        Doc = doc;
        Severity = rule.Severity;
        return Page();
    }
}
