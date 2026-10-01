using Aibysitter.Rules;
using Aibysitter.Rules.PullRequests;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aibysitter.Web.Pages.Notes;

public class IndexModel(LintEngine engine, PullRequestReviewer reviewer) : PageModel
{
    public IReadOnlyList<RuleRow> Rows { get; private set; } = [];

    public IReadOnlyList<RuleRow> CheckRows { get; private set; } = [];

    public void OnGet()
    {
        Rows = engine.Rules.Select(r => new RuleRow(RuleDocs.Find(r.Id)!, r.Severity)).ToList();
        CheckRows = reviewer.Checks.Select(c => new RuleRow(PullRequestCheckDocs.Find(c.Id)!, c.Severity)).ToList();
    }

    public sealed record RuleRow(RuleDoc Doc, Severity Severity);
}
