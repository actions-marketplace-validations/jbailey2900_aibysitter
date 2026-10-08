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

        Assert.DoesNotContain("coming soon", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fail-on-errors", html);
        Assert.Contains("CI config modified: .github/workflows/ci.yml", html);
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
    public async Task CommentKey_OnGitHubPage_PullRequestsWrite_OnPrivacy()
    {
        var client = factory.CreateClient();

        Assert.Contains("<td><code>comment</code></td>", await client.GetStringAsync("/GitHub"));
        Assert.Contains("<td>Read and write</td>\n            <td>Listing changed files and diffs; posting and updating one comment", await client.GetStringAsync("/Privacy"));
    }

    [Fact]
    public async Task GitHubPage_LinksToInstall_InHeroAndInstallSection()
    {
        var html = await factory.CreateClient().GetStringAsync("/GitHub");

        Assert.Equal(2, html.Split("href=\"https://github.com/apps/aibysitter/installations/new\"").Length - 1);
        Assert.Contains(">Install the GitHub App</a>", html);
    }

    [Fact]
    public async Task NoPage_SaysTheAppIsInTesting()
    {
        var client = factory.CreateClient();
        foreach (var path in new[] { "/", "/GitHub", "/About", "/llms.txt" })
        {
            var html = await client.GetStringAsync(path);
            Assert.DoesNotContain("in testing", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("coming soon", html, StringComparison.OrdinalIgnoreCase);
        }
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
