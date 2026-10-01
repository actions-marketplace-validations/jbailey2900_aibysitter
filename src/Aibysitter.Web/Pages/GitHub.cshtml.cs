using Aibysitter.Rules.PullRequests;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aibysitter.Web.Pages;

public class GitHubModel(PullRequestReviewer reviewer) : PageModel
{
    public IReadOnlyList<Notes.IndexModel.RuleRow> Checks =>
        reviewer.Checks.Select(c => new Notes.IndexModel.RuleRow(PullRequestCheckDocs.Find(c.Id)!, c.Severity)).ToList();

    public void OnGet()
    {
    }
}
