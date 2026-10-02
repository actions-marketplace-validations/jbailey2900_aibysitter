using Aibysitter.Rules;
using Aibysitter.Rules.PullRequests;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aibysitter.Web.Pages.Rules;

public class IndexModel(LintEngine engine, PullRequestReviewer reviewer) : PageModel
{
    public IReadOnlyList<RuleRow> Rows { get; private set; } = [];

    public IReadOnlyList<RuleRow> CheckRows { get; private set; } = [];

    public void OnGet()
    {
        var missing = new Aibysitter.Rules.Rules.MissingIdentifiers();
        Rows = engine.Rules.Select(r => new RuleRow(RuleDocs.Find(r.Id)!, r.Severity))
            .Append(new RuleRow(RuleDocs.Find(missing.Id)!, missing.Severity))
            .OrderBy(r => r.Doc.Id, StringComparer.Ordinal)
            .ToList();
        CheckRows = reviewer.Checks.Select(c => new RuleRow(PullRequestCheckDocs.Find(c.Id)!, c.Severity)).ToList();
    }

    public sealed record RuleRow(RuleDoc Doc, Severity Severity);
}
