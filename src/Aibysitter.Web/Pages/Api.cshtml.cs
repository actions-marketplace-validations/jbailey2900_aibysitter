using Aibysitter.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aibysitter.Web.Pages;

public class ApiModel(IConfiguration configuration) : PageModel
{
    public LintRateLimitSettings RateLimit { get; } =
        configuration.GetSection(LintRateLimitSettings.SectionName).Get<LintRateLimitSettings>() ?? new LintRateLimitSettings();

    public void OnGet()
    {
    }
}
