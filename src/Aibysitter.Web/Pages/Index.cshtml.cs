using Aibysitter.Rules;
using Aibysitter.Rules.PullRequests;
using Aibysitter.Web.Samples;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aibysitter.Web.Pages;

public class IndexModel(LintDemo demo, PullRequestReviewer reviewer) : PageModel
{
    public LintDemoResult Demo => demo.Result;

    public IReadOnlyList<RuleDoc> RuleDocs => Aibysitter.Rules.RuleDocs.All;

    public IReadOnlyList<IPullRequestCheck> PullRequestChecks => reviewer.Checks;

    public void OnGet()
    {
    }
}
