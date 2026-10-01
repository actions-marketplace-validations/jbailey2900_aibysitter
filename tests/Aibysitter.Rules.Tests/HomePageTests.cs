using System.Net;
using Aibysitter.Rules.PullRequests;
using Aibysitter.Web.Samples;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aibysitter.Rules.Tests;

public class HomePageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Home_RendersRealLintRunOfSample()
    {
        var html = await factory.CreateClient().GetStringAsync("/");
        var engine = new LintEngine();
        var findings = engine.Lint(SampleRules.Text);
        var score = engine.Score(findings);

        Assert.Contains("aibysitter lint CLAUDE.md", html);
        Assert.Contains($"{findings.Count} findings, score {score.Value}/100 ({score.Grade})", html);
        foreach (var finding in findings)
        {
            Assert.Contains($"CLAUDE.md:{finding.Line}", html);
        }
    }

    [Fact]
    public async Task Home_ListsRuleAndPullRequestChecks()
    {
        var html = await factory.CreateClient().GetStringAsync("/");

        foreach (var doc in RuleDocs.All)
        {
            Assert.Contains($"href=\"/Notes/{doc.Id}\"", html);
        }

        foreach (var check in PullRequestReviewer.DiscoverChecks())
        {
            Assert.Contains($"<a href=\"/Notes/{check.Id}\">{check.Id}</a> {check.Title}", html);
        }
    }

    [Fact]
    public void LintDemo_IsComputedOnceAndCached()
    {
        var demo = factory.Services.GetRequiredService<LintDemo>();

        Assert.Same(demo, factory.Services.GetRequiredService<LintDemo>());
        Assert.Same(demo.Result, demo.Result);
        Assert.NotEmpty(demo.Result.Lines);
    }

    [Theory]
    [InlineData("/fonts/jetbrains-mono-latin-400-normal.woff2", "font/woff2")]
    [InlineData("/fonts/jetbrains-mono-latin-700-normal.woff2", "font/woff2")]
    [InlineData("/fonts/OFL.txt", "text/plain")]
    public async Task FontFiles_AreServedFromSite(string path, string contentType)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
    }
}
