using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests;

public class RulesPageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
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
        var html = await factory.CreateClient().GetStringAsync("/Rules/P003");

        Assert.Contains("<h1>P003 AssertNothingTests</h1>", html);
        Assert.Contains("Pull requests, through the", html);
        Assert.Contains("fails the check when the repo sets fail-on-errors", html);
        Assert.DoesNotContain("per finding", html);
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
    public async Task Methodology_ListsSeveritiesThresholdsAndGaps()
    {
        var html = await factory.CreateClient().GetStringAsync("/Rules/Methodology");

        Assert.Contains("<td>R003, R009, R012</td>", html);
        Assert.Contains("<td>R001, R010, R013, R014, R016</td>", html);
        Assert.Contains("More than 200 lines", html);
        Assert.Contains("129 of 336 (38.4%)", html);
        Assert.Contains("The lowest possible score is 20.", html);
        Assert.Equal(4, html.Split("class=\"gap\"").Length - 1);
    }
}
