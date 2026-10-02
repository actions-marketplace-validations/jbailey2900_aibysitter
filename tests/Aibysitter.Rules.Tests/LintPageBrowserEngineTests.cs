using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests;

public class LintPageBrowserEngineTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string BrowserNote = "Linted in your browser";

    [Fact]
    public async Task Get_LoadsLintPageModule_AndHasResultsContainer()
    {
        var html = await factory.CreateClient().GetStringAsync("/Lint");

        Assert.Matches(@"<script type=""module"" src=""/js/lint-page(\.\w+)?\.mjs(\?v=[^""]+)?""></script>", html);
        Assert.Contains(@"<form method=""post"" id=""lint-form"">", html);
        Assert.Contains(@"<div id=""lint-results"" aria-live=""polite"">", html);
    }

    [Fact]
    public async Task ServerPost_DoesNotClaimBrowserLinting()
    {
        var response = await LintClient.PostAsync(factory.CreateClient(), "# Rules\n- Always use tabs.\n- Never use tabs.");
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, LintClient.Snippet(html));
        Assert.Contains("<h2>Findings (1)</h2>", html);
        Assert.DoesNotContain(BrowserNote, html);
    }

    [Theory]
    [InlineData("/js/lint-page.mjs")]
    [InlineData("/js/lint-engine.mjs")]
    [InlineData("/js/generated/rules-patterns.mjs")]
    public async Task Modules_AreServedAsJavaScript(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.True(response.IsSuccessStatusCode, $"{path} returned {(int)response.StatusCode}");
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GeneratedPatterns_MatchCurrentExport()
    {
        var served = await factory.CreateClient().GetStringAsync("/js/generated/rules-patterns.mjs");

        Assert.Equal(Browser.PatternExport.Build(), served);
    }
}
