using Aibysitter.Rules;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aibysitter.Web.Pages.Rules;

public class MethodologyModel(LintEngine engine) : PageModel
{
    /// <summary>Lint rule IDs per severity, plus R006 (App-only).</summary>
    public IReadOnlyDictionary<Severity, IReadOnlyList<string>> RulesBySeverity { get; private set; } = new Dictionary<Severity, IReadOnlyList<string>>();

    public void OnGet()
    {
        var missing = new Aibysitter.Rules.Rules.MissingIdentifiers();
        RulesBySeverity = engine.Rules.Select(r => (r.Id, r.Severity))
            .Append((missing.Id, missing.Severity))
            .GroupBy(r => r.Severity)
            .OrderByDescending(g => g.Key)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(r => r.Id).Order(StringComparer.Ordinal).ToList());
    }
}
