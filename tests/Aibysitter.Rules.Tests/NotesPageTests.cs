using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests;

public class NotesPageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Index_ListsEveryRule_WithLink()
    {
        var html = await factory.CreateClient().GetStringAsync("/Notes");

        foreach (var doc in RuleDocs.All)
        {
            Assert.Contains($"href=\"/Notes/{doc.Id}\"", html);
            Assert.Contains(doc.Name, html);
        }
    }

    [Fact]
    public async Task RulePage_RendersDoc()
    {
        var html = await factory.CreateClient().GetStringAsync("/Notes/R003");

        Assert.Contains("<h1>R003 ContradictoryModals</h1>", html);
        Assert.Contains("Error", html);
        Assert.Contains("- Never use tabs.", html);
    }

    [Fact]
    public async Task Index_ListsPullRequestChecks()
    {
        var html = await factory.CreateClient().GetStringAsync("/Notes");

        foreach (var doc in Aibysitter.Rules.PullRequests.PullRequestCheckDocs.All)
        {
            Assert.Contains($"href=\"/Notes/{doc.Id}\"", html);
        }
    }

    [Fact]
    public async Task CheckPage_RendersPullRequestDoc()
    {
        var html = await factory.CreateClient().GetStringAsync("/Notes/P003");

        Assert.Contains("<h1>P003 AssertNothingTests</h1>", html);
        Assert.Contains("Pull requests, through the", html);
        Assert.Contains("fails the check when the repo sets fail-on-errors", html);
        Assert.DoesNotContain("per finding", html);
    }

    [Theory]
    [InlineData("/Notes/R999")]
    [InlineData("/Notes/P999")]
    [InlineData("/Notes/R006")]
    public async Task RulePage_UnknownId_Returns404(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Index_DocumentsSeverityCapsAndSuppression()
    {
        var html = await factory.CreateClient().GetStringAsync("/Notes");

        Assert.Contains("Error 40, Warning 30, Info 10", html);
        Assert.Contains("&lt;!-- aibysitter-disable-next-line R001, R005 --&gt;", html);
    }
}
