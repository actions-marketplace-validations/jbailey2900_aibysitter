using Aibysitter.Rules;
using Aibysitter.Rules.PullRequests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aibysitter.Web.Pages.Notes;

public class RuleModel(LintEngine engine, PullRequestReviewer reviewer) : PageModel
{
    public RuleDoc Doc { get; private set; } = null!;

    public Severity Severity { get; private set; }

    public bool IsPullRequestCheck { get; private set; }

    public IActionResult OnGet(string id)
    {
        if (RuleDocs.Find(id) is { } ruleDoc
            && engine.Rules.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase)) is { } rule)
        {
            Doc = ruleDoc;
            Severity = rule.Severity;
            return Page();
        }

        if (PullRequestCheckDocs.Find(id) is { } checkDoc
            && reviewer.Checks.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)) is { } check)
        {
            Doc = checkDoc;
            Severity = check.Severity;
            IsPullRequestCheck = true;
            return Page();
        }

        return NotFound();
    }
}
