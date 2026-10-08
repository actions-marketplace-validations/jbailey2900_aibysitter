using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace Aibysitter.Web.Pages;

/// <summary>Body for empty GET/HEAD error responses, re-executed by status code pages. The original status code is kept.</summary>
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class StatusCodeModel : PageModel
{
    public int Code { get; private set; }

    public string Title => Code == StatusCodes.Status404NotFound ? "Page not found." : $"HTTP {Code}. {ReasonPhrases.GetReasonPhrase(Code)}";

    public IActionResult OnGet(int code)
    {
        if (HttpContext.Features.Get<IStatusCodeReExecuteFeature>() is null)
        {
            return NotFound();
        }

        Code = code;
        return Page();
    }
}
