using Aibysitter.Rules;
using Aibysitter.Rules.PullRequests;
using Aibysitter.Web.Gallery;
using Aibysitter.Web.Samples;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aibysitter.Web.Pages;

public class IndexModel(LintDemo demo, PullRequestReviewer reviewer, GalleryCatalog gallery) : PageModel
{
    public LintDemoResult Demo => demo.Result;

    public IReadOnlyList<RuleDoc> LintRuleDocs => RuleDocs.All.Where(d => !d.AppOnly).ToList();

    public IReadOnlyList<RuleDoc> AppOnlyRuleDocs => RuleDocs.All.Where(d => d.AppOnly).ToList();

    public IReadOnlyList<IPullRequestCheck> PullRequestChecks => reviewer.Checks;

    public IReadOnlyList<GalleryEntry> GalleryEntries => gallery.All;

    public void OnGet()
    {
    }
}
