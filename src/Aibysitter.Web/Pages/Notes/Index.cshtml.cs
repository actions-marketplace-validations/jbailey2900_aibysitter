using Aibysitter.Rules;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aibysitter.Web.Pages.Notes;

public class IndexModel(LintEngine engine) : PageModel
{
    public IReadOnlyList<RuleRow> Rows { get; private set; } = [];

    public void OnGet()
    {
        Rows = engine.Rules
            .Select(r => new RuleRow(RuleDocs.Find(r.Id)!, r.Severity))
            .ToList();
    }

    public sealed record RuleRow(RuleDoc Doc, Severity Severity);
}
