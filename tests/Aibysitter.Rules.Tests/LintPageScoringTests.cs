using System.Net;
using Aibysitter.Web.Samples;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests;

public class LintPageScoringTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task PostFixture_RendersScoreAndDeductions()
    {
        var fixture = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "R003-contradictory-modals.md"));

        var html = await (await LintClient.PostAsync(factory.CreateClient(), fixture)).Content.ReadAsStringAsync();

        Assert.True(html.Contains("80 / 100 — B"), $"Score missing: {LintClient.Snippet(html)}");
        Assert.Contains("class=\"score grade-b\"", html);
        Assert.Contains("<td>−20</td>", html);
    }

    [Fact]
    public async Task SuppressedFindings_ListedSeparately_NotScored()
    {
        var html = await (await LintClient.PostAsync(factory.CreateClient(), "<!-- aibysitter-disable R002 -->\n- Handle errors.\n")).Content.ReadAsStringAsync();

        Assert.Contains("100 / 100 — A", html);
        Assert.Contains("<h2>Findings (0)</h2>", html);
        Assert.Contains("<h2>Suppressed (1)</h2>", html);
    }

    [Fact]
    public async Task SeverityCap_ShownWhenApplied()
    {
        // R002: 8 × 4 = 32, per-rule cap 30. R005: 2 × 4 = 8. Warning total 38, cap 30.
        var text = "# Rules\n\n" + string.Join("\n", Enumerable.Range(1, 8).Select(n => $"- Handle case {n}.")) + "\n- Use tabs for indentation.\n- Use tabs for indentation.\n- Use tabs for indentation.\n";

        var html = await (await LintClient.PostAsync(factory.CreateClient(), text)).Content.ReadAsStringAsync();

        Assert.Contains("<td>Warning cap</td>", html);
        Assert.Contains("<td>−30 (of −38)</td>", html);
        Assert.Contains("70 / 100 — C", html);
    }

    [Fact]
    public async Task PostClean_Scores100_NoDeductions()
    {
        var html = await (await LintClient.PostAsync(factory.CreateClient(), "# Rules\n\n- Use tabs.\n")).Content.ReadAsStringAsync();

        Assert.Contains("100 / 100 — A", html);
        Assert.Contains("No deductions.", html);
    }

    [Fact]
    public async Task Findings_LinkToRuleNotes()
    {
        var fixture = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "R003-contradictory-modals.md"));

        var html = await (await LintClient.PostAsync(factory.CreateClient(), fixture)).Content.ReadAsStringAsync();

        Assert.Contains("<a href=\"/Notes/R003\">R003</a> Contradictory modals", html);
    }

    [Fact]
    public async Task SampleLink_PrefillsTextarea()
    {
        var client = factory.CreateClient();

        Assert.Contains("href=\"/Lint?sample=true\"", await client.GetStringAsync("/Lint"));

        var response = await client.GetAsync("/Lint?sample=true");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("- Always run the formatter before commit.", html);
        Assert.DoesNotContain("class=\"score", html);
    }

    [Fact]
    public async Task Sample_TriggersR001R002R003R005_Scores76()
    {
        var html = await (await LintClient.PostAsync(factory.CreateClient(), SampleRules.Text)).Content.ReadAsStringAsync();

        foreach (var id in new[] { "R001", "R002", "R003", "R005" })
        {
            Assert.Contains($"<a href=\"/Notes/{id}\">{id}</a>", html);
        }

        Assert.True(html.Contains("76 / 100 — C"), $"Score missing: {LintClient.Snippet(html)}");
    }
}
