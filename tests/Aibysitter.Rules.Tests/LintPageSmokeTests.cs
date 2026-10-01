using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests;

public partial class LintPageSmokeTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task PostFixture_RendersFindings()
    {
        var fixture = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "R003-contradictory-modals.md"));

        var response = await LintClient.PostAsync(factory.CreateClient(), fixture);
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"POST /Lint returned {(int)response.StatusCode}: {LintClient.Snippet(html)}");
        Assert.True(html.Contains("<h2>Findings (2)</h2>"), $"Findings header missing: {LintClient.Snippet(html)}");
        Assert.Equal(new[] { "8", "9" }, LineCellRegex().Matches(html).Select(m => m.Groups[1].Value));
    }

    [GeneratedRegex(@"<tr class=""sev-error"">\s*<td>(\d+)</td>")]
    private static partial Regex LineCellRegex();
}
