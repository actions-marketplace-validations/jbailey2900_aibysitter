using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests;

public class LintPageBrowserEngineTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string BrowserNote = "The text was not sent.";

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
    [InlineData("/js/share-link.mjs")]
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

    [Fact]
    public async Task Sample_RendersSixFindingsOnLoad_WithNote()
    {
        var html = await factory.CreateClient().GetStringAsync("/Lint?sample=true");

        Assert.Contains("This sample intentionally triggers six findings.", html);
        Assert.Contains("<h2>Findings (6)</h2>", html);
        Assert.Contains("Linted as Markdown rules file (detected).", html);
    }

    [Fact]
    public async Task PlainGet_AndPost_HaveNoSampleNote()
    {
        var client = factory.CreateClient();
        var get = await client.GetStringAsync("/Lint");
        var post = await (await LintClient.PostAsync(client, Web.Samples.SampleRules.Text)).Content.ReadAsStringAsync();

        Assert.DoesNotContain("intentionally triggers", get);
        Assert.DoesNotContain("<h2>Findings", get);
        Assert.DoesNotContain("intentionally triggers", post);
        Assert.Contains("<h2>Findings (6)</h2>", post);
    }

    [Fact]
    public async Task Form_HasFormatAndPrivacyLines()
    {
        var html = await factory.CreateClient().GetStringAsync("/Lint");

        Assert.Contains("The format only changes whether R015 and R016 run.", html);
        Assert.Contains("Linted in your browser; the text is not sent.", html);
        Assert.Contains(@"<a href=""/Privacy"">Privacy</a>", html);
    }

    private static IEnumerable<KeyValuePair<string, string>> RulesExcept(params string[] off) =>
        new LintEngine().Rules.Where(r => !off.Contains(r.Id)).Select(r => KeyValuePair.Create("Enabled", r.Id))
            .Prepend(KeyValuePair.Create("RulesPosted", "true"));

    [Fact]
    public async Task Form_HasCollapsedRulesPicker_AllChecked()
    {
        var html = await factory.CreateClient().GetStringAsync("/Lint");
        var rules = new LintEngine().Rules;

        Assert.Contains("<details class=\"rules-picker\">", html);
        Assert.Equal(rules.Count, html.Split("name=\"Enabled\"").Length - 1);
        Assert.Equal(rules.Count, html.Split("checked=\"checked\"").Length - 1);
        Assert.DoesNotContain("value=\"R006\"", html);
    }

    [Fact]
    public async Task ServerPost_UncheckedRule_IsDisabled_AndEchoed()
    {
        var response = await LintClient.PostAsync(factory.CreateClient(), Web.Samples.SampleRules.Text, RulesExcept("R001"));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<h2>Findings (4)</h2>", html);
        Assert.Contains("<p class=\"note\">Rules off: R001.</p>", html);
        Assert.DoesNotContain("<a href=\"/Rules/R001\">R001</a> Rationale prose", html);
        Assert.Contains("<details class=\"rules-picker\" open=\"open\">", html);
        Assert.Contains("value=\"R001\" /> R001", html);
        Assert.Contains("value=\"R002\" checked=\"checked\"", html);
    }

    [Fact]
    public async Task ServerPost_WithoutRulesSection_RunsEveryRule()
    {
        var html = await (await LintClient.PostAsync(factory.CreateClient(), Web.Samples.SampleRules.Text)).Content.ReadAsStringAsync();

        Assert.Contains("<h2>Findings (6)</h2>", html);
        Assert.DoesNotContain("Rules off", html);
    }
}
