using Aibysitter.Rules.PullRequests;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests;

public class GitHubPageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GitHubPage_ListsChecks_ConfigAndComingSoon()
    {
        var html = await factory.CreateClient().GetStringAsync("/GitHub");

        foreach (var doc in PullRequestCheckDocs.All)
        {
            Assert.Contains($"<a href=\"/Rules/{doc.Id}\">{doc.Id}</a>", html);
        }

        Assert.Contains("Install coming soon.", html);
        Assert.Contains("fail-on-errors", html);
        Assert.Contains("Multiply_Works", html);
    }

    [Fact]
    public async Task CheckRunName_IsAibysitterReview_OnPageAndPrivacy()
    {
        var client = factory.CreateClient();

        Assert.Equal("Aibysitter review", Aibysitter.Web.GitHub.GitHubOptions.CheckRunName);
        Assert.Contains("It posts a check named Aibysitter review, with", await client.GetStringAsync("/GitHub"));
        Assert.Contains("<td>Creating and completing the Aibysitter review check run</td>", await client.GetStringAsync("/Privacy"));
    }

    [Fact]
    public async Task GitHubPage_HasNoInstallLink_UntilAppIsPublic()
    {
        var html = await factory.CreateClient().GetStringAsync("/GitHub");

        Assert.DoesNotContain("installations/new", html);
        Assert.DoesNotContain("github.com/apps", html);
    }

    [Fact]
    public async Task Nav_LinksToGitHubPage()
    {
        Assert.Contains("href=\"/GitHub\">github</a>", await factory.CreateClient().GetStringAsync("/"));
    }

    [Fact]
    public async Task Configuration_DocumentsDisable()
    {
        var html = await factory.CreateClient().GetStringAsync("/GitHub");

        Assert.Contains("<td><code>disable</code></td>", html);
    }
}
