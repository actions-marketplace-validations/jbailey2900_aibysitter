using System.ComponentModel.DataAnnotations;
using Aibysitter.Rules;
using Aibysitter.Web.Gallery;
using Aibysitter.Web.Samples;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aibysitter.Web.Pages;

public class LintModel(LintEngine engine, GalleryCatalog galleryCatalog, ILogger<LintModel> logger) : PageModel
{
    public const int MaxContentLength = 100_000;

    [BindProperty]
    [Required(ErrorMessage = "Paste a rules file to lint.")]
    [StringLength(MaxContentLength, ErrorMessage = "Input is limited to 100,000 characters.")]
    public string? RulesText { get; set; }

    public IReadOnlyList<ResultRow>? Results { get; private set; }

    public LintScore? Score { get; private set; }

    public IReadOnlyList<ResultRow>? Suppressed { get; private set; }

    /// <summary>Prefills the textarea from the sample (<c>?sample=true</c>) or a gallery entry (<c>?gallery=&lt;id&gt;</c>).</summary>
    public void OnGet(bool sample = false, string? gallery = null)
    {
        if (sample)
        {
            RulesText = SampleRules.Text;
        }
        else if (gallery is not null && galleryCatalog.Find(gallery) is { } entry)
        {
            RulesText = entry.Content;
        }
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var rules = engine.Rules.ToDictionary(r => r.Id);
        var result = engine.Analyze(RulesText!);

        Results = result.Findings
            .Select(f => new ResultRow(f, rules[f.RuleId].Title, rules[f.RuleId].Severity))
            .ToList();

        Suppressed = result.Suppressed
            .Select(f => new ResultRow(f, rules[f.RuleId].Title, rules[f.RuleId].Severity))
            .ToList();

        Score = engine.Score(result.Findings);

        logger.LogInformation("Linted {Length} chars, {FindingCount} findings, {SuppressedCount} suppressed, score {Score}", RulesText!.Length, Results.Count, Suppressed.Count, Score.Value);

        return Page();
    }

    public sealed record ResultRow(Finding Finding, string Title, Severity Severity);
}
