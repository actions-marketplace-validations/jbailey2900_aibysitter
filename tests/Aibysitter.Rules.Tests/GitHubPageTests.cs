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
            Assert.Contains($"<a href=\"/Notes/{doc.Id}\">{doc.Id}</a>", html);
        }

        Assert.Contains("Install coming soon.", html);
        Assert.Contains("fail-on-errors", html);
        Assert.Contains("Multiply_Works", html);
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
}
