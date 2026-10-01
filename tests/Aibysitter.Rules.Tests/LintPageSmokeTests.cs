using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests;

public partial class LintPageSmokeTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task PostFixture_RendersFindings()
    {
        var client = factory.CreateClient();

        var form = await client.GetStringAsync("/Lint");
        var token = TokenRegex().Match(form).Groups[1].Value;
        Assert.NotEmpty(token);

        var fixture = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "R003-contradictory-modals.md"));
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["RulesText"] = fixture,
        });

        var response = await client.PostAsync("/Lint", content);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<h2>Findings (2)</h2>", html);
        Assert.Equal(new[] { "8", "9" }, LineCellRegex().Matches(html).Select(m => m.Groups[1].Value));
    }

    [GeneratedRegex(@"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"<tr class=""sev-error"">\s*<td>(\d+)</td>")]
    private static partial Regex LineCellRegex();
}
