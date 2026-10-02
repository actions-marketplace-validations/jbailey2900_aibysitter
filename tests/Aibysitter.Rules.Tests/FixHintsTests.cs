using Aibysitter.Rules.PullRequests;
using Aibysitter.Web.Linting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aibysitter.Rules.Tests;

public class FixHintsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly RuleFixHints Hints = new(new LintEngine(), new PullRequestReviewer());

    public static TheoryData<string> AllIds() => [.. RuleDocs.All.Select(d => d.Id).Concat(PullRequestCheckDocs.All.Select(d => d.Id))];

    [Theory]
    [MemberData(nameof(AllIds))]
    public void EveryRuleAndCheck_HasFixFromItsBadExample(string id)
    {
        Assert.NotEmpty(Hints.For(id));
        Assert.All(Hints.For(id), h => Assert.False(string.IsNullOrWhiteSpace(h)));
    }

    [Fact]
    public void LintRuleHint_EqualsWhatTheEngineEmits()
    {
        var doc = RuleDocs.Find("R003")!;

        Assert.Equal(new LintEngine().Lint(doc.BadExample).Where(f => f.RuleId == "R003").Select(f => f.FixHint).Distinct(), Hints.For("R003"));
    }

    [Fact]
    public void UnknownId_HasNoHints() => Assert.Empty(Hints.For("R099"));

    [Theory]
    [MemberData(nameof(AllIds))]
    public async Task RulePage_ShowsBeforeAfterFix(string id)
    {
        var html = await factory.CreateClient().GetStringAsync($"/Rules/{id}");

        Assert.Contains("<h2>Before</h2>", html);
        Assert.Contains("<h2>After</h2>", html);
        Assert.DoesNotContain("<h2>Flagged</h2>", html);
        Assert.Contains("<h2>Fix</h2>", html);
        Assert.All(Hints.For(id), h => Assert.Contains($"<li>{System.Text.Encodings.Web.HtmlEncoder.Default.Encode(h)}</li>", html));
    }

    [Fact]
    public async Task SourceView_ShowsFixUnderEachFinding()
    {
        var catalog = new Web.Gallery.GalleryCatalog(new LintEngine(), typeof(FixHintsTests).Assembly);
        var entry = catalog.Find("flawed-example");
        var client = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<Web.Gallery.GalleryCatalog>();
            s.AddSingleton(catalog);
        })).CreateClient();
        var html = await client.GetStringAsync("/Gallery/flawed-example/source");

        Assert.NotNull(entry);
        Assert.NotEmpty(entry.Findings);
        Assert.All(entry.Findings, f => Assert.Contains($"<span class=\"file-fix\">Fix: {System.Text.Encodings.Web.HtmlEncoder.Default.Encode(f.Finding.FixHint)}</span>", html));
    }
}
