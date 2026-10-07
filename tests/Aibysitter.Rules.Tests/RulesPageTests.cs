using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aibysitter.Rules.Tests;

public class RulesPageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task R001_ShowsKnownLimits_R002_DoesNot()
    {
        var client = factory.CreateClient();
        var r001 = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync("/Rules/R001"));
        var r002 = await client.GetStringAsync("/Rules/R002");

        Assert.Contains("<dt>Known limits</dt>", r001);
        Assert.Contains("(\"never skip steps because a task seems small\") is flagged as rationale.", r001);
        Assert.Contains("A descriptive sentence that shares a list item with an instruction is flagged as rationale.", r001);
        Assert.DoesNotContain("<dt>Known limits</dt>", r002);
    }

    [Fact]
    public async Task Index_ListsEveryRule_WithLink()
    {
        var html = await factory.CreateClient().GetStringAsync("/Rules");

        foreach (var doc in RuleDocs.All)
        {
            Assert.Contains($"href=\"/Rules/{doc.Id}\"", html);
            Assert.Contains(doc.Name, html);
        }
    }

    [Fact]
    public async Task RulePage_RendersDoc()
    {
        var html = await factory.CreateClient().GetStringAsync("/Rules/R003");

        Assert.Contains("<h1>R003 ContradictoryModals</h1>", html);
        Assert.Contains("Error", html);
        Assert.Contains("- Never use tabs.", html);
    }

    [Fact]
    public async Task Index_ListsPullRequestChecks()
    {
        var html = await factory.CreateClient().GetStringAsync("/Rules");

        foreach (var doc in Aibysitter.Rules.PullRequests.PullRequestCheckDocs.All)
        {
            Assert.Contains($"href=\"/Rules/{doc.Id}\"", html);
        }
    }

    [Fact]
    public async Task CheckPage_RendersPullRequestDoc()
    {
        var html = await factory.CreateClient().GetStringAsync("/Rules/P001");

        Assert.Contains("<h1>P001 PlaceholderIdentifiers</h1>", html);
        Assert.Contains("Pull requests, through the", html);
        Assert.Contains("fails the check when the repo sets fail-on-errors", html);
        Assert.DoesNotContain("per finding", html);
    }

    [Fact]
    public async Task CheckPage_P003_IsInfo()
    {
        var html = await factory.CreateClient().GetStringAsync("/Rules/P003");

        Assert.Contains("<dd>Info</dd>", html);
        Assert.DoesNotContain("fails the check when the repo sets fail-on-errors", html);
    }

    [Theory]
    [InlineData("/Rules/R999")]
    [InlineData("/Rules/P999")]
    [InlineData("/Rules/R099")]
    public async Task RulePage_UnknownId_Returns404(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Index_DocumentsSeverityCapsAndSuppression()
    {
        var html = await factory.CreateClient().GetStringAsync("/Rules");

        Assert.Contains("Error 40, Warning 30, Info 10", html);
        Assert.Contains("&lt;!-- aibysitter-disable-next-line R001, R005 --&gt;", html);
    }

    [Fact]
    public async Task R006_ListedAsAppOnly_PageExplainsP014()
    {
        var client = factory.CreateClient();

        Assert.Contains("Warning (App-only)", await client.GetStringAsync("/Rules"));

        var page = await client.GetStringAsync("/Rules/R006");
        Assert.Contains("through P014", page);
        Assert.Contains("Warning (P014 finding; not scored)", page);
    }

    [Theory]
    [InlineData("/Notes/R003", "/Rules/R003")]
    [InlineData("/Notes/p003", "/Rules/P003")]
    public async Task OldRuleUrl_RedirectsPermanently(string from, string to)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(from);

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal(to, response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task UnknownNote_Returns404()
    {
        var response = await factory.CreateClient().GetAsync("/Notes/no-such-note");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Lint?sample=true")]
    [InlineData("/Rules")]
    [InlineData("/GitHub")]
    [InlineData("/Gallery")]
    [InlineData("/Gallery/aspnet-web-api")]
    public async Task Pages_DoNotLinkOldRuleUrls(string path)
    {
        var html = await factory.CreateClient().GetStringAsync(path);

        Assert.DoesNotMatch(@"href=""/Notes/[PR]\d{3}""", html);
    }

    [Theory]
    [InlineData("/Rules/R002", "Pattern-based: instructions use verbs with no checkable outcome.")]
    [InlineData("/Rules/R003", "Mirrored instructions: the same instruction is both required and forbidden.")]
    [InlineData("/Rules/R015", "Apply Manually")]
    [InlineData("/Rules/R015", "Apply Intelligently")]
    [InlineData("/Rules/R015", "Apply to Specific Files")]
    [InlineData("/Rules/R015", "Always Apply")]
    [InlineData("/Rules", "they cost tokens on every run")]
    [InlineData("/Rules", "href=\"/Rules/Methodology\"")]
    public async Task RuleCopy(string path, string expected)
    {
        Assert.Contains(expected, await factory.CreateClient().GetStringAsync(path));
    }

    [Fact]
    public async Task CursorModes_OnlyOnR015()
    {
        Assert.DoesNotContain("Cursor application modes", await factory.CreateClient().GetStringAsync("/Rules/R014"));
    }

    [Fact]
    public async Task Methodology_ListsSeveritiesThresholdsAndPositions()
    {
        var html = await factory.CreateClient().GetStringAsync("/Rules/Methodology");

        Assert.Contains("<td>R003, R009, R012</td>", html);
        Assert.Contains("<td>R001, R010, R013, R014, R016</td>", html);
        Assert.Contains("More than 200 lines", html);
        Assert.Contains("116 of 755 (15.4%)", html);
        Assert.DoesNotContain("336", html);
        Assert.Contains("The lowest possible score is 20.", html);
        var decoded = System.Net.WebUtility.HtmlDecode(html);
        Assert.Contains("The reason belongs in a commit message or a design note, where a human reads it; the rules file gets the rule.", decoded);
        Assert.Contains("<h4>R004, 200 lines</h4>", decoded);
        Assert.Contains("<h4>R008, emphasis</h4>", decoded);
        Assert.Contains("<h4>R013, paragraphs</h4>", decoded);
    }

    [Theory]
    [InlineData("R001", "47% (14 of 30) and 40% (12 of 30), two judges, ruleset v4.")]
    [InlineData("R005", "17% (1 of 6) and 17% (1 of 6), two judges, ruleset v4.")]
    [InlineData("R004", "0% (0 of 30) and 0% (0 of 30), two judges, ruleset v2; rule unchanged since.")]
    [InlineData("R003", "Not measured: no findings in the 755-file corpus.")]
    [InlineData("R006", "Not measured per finding (App-only; judged per file in field note 1).")]
    public async Task RulePage_ShowsMeasuredFalsePositiveRate(string id, string text)
    {
        var html = await factory.CreateClient().GetStringAsync($"/Rules/{id}");

        Assert.Contains("<dt>Measured false-positive rate</dt>", html);
        Assert.Contains(text, System.Net.WebUtility.HtmlDecode(html));
        Assert.Contains("href=\"/Rules/Methodology#false-positive-rates\"", html);
    }

    [Fact]
    public void EveryLintRuleAndR006_HasMeasurementRow()
    {
        var ids = new LintEngine().Rules.Select(r => r.Id).Append("R006").Order(StringComparer.Ordinal);

        Assert.Equal(ids, Web.Linting.RuleMeasurements.RuleIds);
    }

    [Fact]
    public void Measurements_ReviewedForCurrentRuleset()
    {
        Assert.Equal(RulesetVersion.Current, Web.Linting.RuleMeasurements.ReviewedAtRuleset);
    }

    [Fact]
    public async Task Methodology_HasFalsePositiveSection_WithEveryRule()
    {
        var html = await factory.CreateClient().GetStringAsync("/Rules/Methodology");

        Assert.Contains("<h2 id=\"false-positive-rates\">Measured false-positive rates</h2>", html);
        Assert.Contains("140 of 149 findings (94.0%), Cohen's kappa 0.84.", System.Net.WebUtility.HtmlDecode(html));
        Assert.All(Web.Linting.RuleMeasurements.RuleIds, id => Assert.Contains($"href=\"/Rules/{id}\"", html));
    }

    [Fact]
    public async Task PullRequestCheckPage_HasNoMeasurementRow()
    {
        Assert.DoesNotContain("Measured false-positive rate", await factory.CreateClient().GetStringAsync("/Rules/P001"));
    }

    [Fact]
    public async Task NoPageRendersAPlaceholder()
    {
        var client = factory.CreateClient();
        var paths = factory.Services.GetRequiredService<Web.Seo.SiteMap>().Entries.Select(e => e.Path).ToList();

        Assert.NotEmpty(paths);
        foreach (var path in paths)
        {
            var html = await client.GetStringAsync(path);
            Assert.False(html.Contains("Position to be written", StringComparison.OrdinalIgnoreCase) || html.Contains("class=\"gap\"", StringComparison.Ordinal), $"Placeholder on {path}");
        }
    }
}
