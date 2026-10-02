using System.ComponentModel.DataAnnotations;
using Aibysitter.Rules;
using Aibysitter.Web.Gallery;
using Aibysitter.Web.Linting;
using Aibysitter.Web.Samples;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aibysitter.Web.Pages;

public class LintModel(LintService lint, GalleryCatalog galleryCatalog) : PageModel
{
    public const int MaxContentLength = LintLimits.MaxContentLength;

    [BindProperty]
    [Required(ErrorMessage = "Paste a rules file to lint.")]
    [StringLength(MaxContentLength, ErrorMessage = "Input is limited to 100,000 characters.")]
    public string? RulesText { get; set; }

    [BindProperty]
    public RulesFormat Format { get; set; } = RulesFormat.Auto;

    /// <summary>Format the last lint ran as (Auto resolved).</summary>
    public RulesFormat? LintedFormat { get; private set; }

    public IEnumerable<SelectListItem> FormatOptions =>
        RulesFormats.Selectable.Select(f => new SelectListItem(RulesFormats.DisplayName(f), f.ToString(), f == Format));

    public IReadOnlyList<LintRow>? Results { get; private set; }

    public LintScore? Score { get; private set; }

    public IReadOnlyList<LintRow>? Suppressed { get; private set; }

    /// <summary>Checked rule IDs from the Rules section. Posted with <see cref="RulesPosted"/>; unchecked rules are disabled.</summary>
    [BindProperty]
    public List<string> Enabled { get; set; } = [];

    /// <summary>Marks that the Rules section was posted, so an empty <see cref="Enabled"/> means every rule is off.</summary>
    [BindProperty]
    public bool RulesPosted { get; set; }

    public IReadOnlyList<IRule> Rules => lint.Rules;

    /// <summary>Rules disabled for the last lint.</summary>
    public IReadOnlyList<string> Disabled { get; private set; } = [];

    public bool IsEnabled(string ruleId) => !Disabled.Contains(ruleId);

    /// <summary>True when the page shows the built-in sample's results on load.</summary>
    public bool IsSample { get; private set; }

    /// <summary>Prefills the textarea from the sample (<c>?sample=true</c>) or a gallery entry (<c>?gallery=&lt;id&gt;</c>).</summary>
    public void OnGet(bool sample = false, string? gallery = null)
    {
        if (sample)
        {
            RulesText = SampleRules.Text;
            IsSample = true;
            Lint();
        }
        else if (gallery is not null && galleryCatalog.Find(gallery) is { } entry)
        {
            RulesText = entry.Content;
            Format = entry.Format;
        }
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        Lint();
        return Page();
    }

    private void Lint()
    {
        var off = RulesPosted ? lint.Rules.Select(r => r.Id).Except(Enabled, StringComparer.OrdinalIgnoreCase) : Enumerable.Empty<string>();
        Disabled = lint.TryNormalizeDisabled(off, out var disabled, out _) ? disabled : [];
        var outcome = lint.Lint(RulesText!, Format, Disabled, "form");
        LintedFormat = outcome.Format;
        Results = outcome.Findings;
        Suppressed = outcome.Suppressed;
        Score = outcome.Score;
    }
}
