using Aibysitter.Rules;
using Aibysitter.Rules.PullRequests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aibysitter.Web.Pages.Rules;

public class RuleModel(LintEngine engine, PullRequestReviewer reviewer, Aibysitter.Web.Linting.RuleFixHints fixHints) : PageModel
{
    /// <summary>What the rule tells you to do, from its own bad example.</summary>
    public IReadOnlyList<string> FixHints => fixHints.For(Doc.Id);

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

        if (RuleDocs.Find(id) is { AppOnly: true } appOnlyDoc)
        {
            Doc = appOnlyDoc;
            Severity = new Aibysitter.Rules.Rules.MissingIdentifiers().Severity;
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
