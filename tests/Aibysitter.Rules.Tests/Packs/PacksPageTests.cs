using System.Net;
using System.Text.Json;
using Aibysitter.Packs;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests.Packs;

public class PacksPageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly PackCatalog Catalog = new();
    private static readonly LintEngine Engine = new();

    private static LintScore SectionScore(PackSection s) => Engine.Score(Engine.Lint(s.Markdown, RulesFormat.Markdown));

    private static LintScore PackScore(Pack p) => Engine.Score(Engine.Lint(PackComposer.Compose([p], RulesFormat.ClaudeMd), RulesFormat.ClaudeMd));

    [Fact]
    public async Task Page_ListsEveryPack_SectionScores_InitCommand()
    {
        var html = await factory.CreateClient().GetStringAsync("/Packs");

        foreach (var pack in Catalog.All)
        {
            var score = PackScore(pack);
            Assert.Contains($"<li class=\"entry pack\" id=\"{pack.Id}\">", html);
            Assert.Contains($"<h2>{WebUtility.HtmlEncode(pack.Manifest.Title)}</h2>", html);
            Assert.Contains($"<code>aibysitter init --packs {pack.Id} --format claude</code>", html);
            Assert.Contains($"{score.Grade} {score.Value}</span>", html);
            foreach (var section in pack.Sections)
            {
                var s = SectionScore(section);
                Assert.Contains($"<span class=\"pack-section-heading\">{WebUtility.HtmlEncode(section.Heading)}</span> <span class=\"grade grade-{s.Grade.ToLowerInvariant()}\">{s.Grade} {s.Value}</span>", html);
            }
        }
    }

    [Fact]
    public async Task Page_RendersSectionMarkdown_WithoutRawHtml()
    {
        var html = await factory.CreateClient().GetStringAsync("/Packs");

        Assert.Contains("<li>Build: <code>dotnet build -warnaserror</code></li>", html);
        Assert.DoesNotContain("<h2>Commands</h2>", html);
    }

    [Fact]
    public async Task Nav_And_Gallery_LinkToPacks()
    {
        var client = factory.CreateClient();

        Assert.Contains("<li><a href=\"/Packs\">packs</a></li>", await client.GetStringAsync("/"));
        Assert.Contains("<a href=\"/Packs\">rules packs</a>", await client.GetStringAsync("/Gallery"));
    }

    [Fact]
    public async Task Registry_Shape_And_Scores()
    {
        var response = await factory.CreateClient().GetAsync("/packs/registry.json");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=300", response.Headers.CacheControl?.ToString());
        Assert.Equal(1, json.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(RulesetVersion.Current, json.GetProperty("rulesetVersion").GetInt32());
        var packs = json.GetProperty("packs").EnumerateArray().ToList();
        Assert.Equal(Catalog.All.Select(p => p.Id), packs.Select(p => p.GetProperty("id").GetString()));

        var api = packs.Single(p => p.GetProperty("id").GetString() == "aspnet-web-api");
        var pack = Catalog.Find("aspnet-web-api")!;
        Assert.Equal(PackScore(pack).Value, api.GetProperty("score").GetInt32());
        Assert.Equal("https://aibysitting.net/Packs#aspnet-web-api", api.GetProperty("pageUrl").GetString());
        Assert.Equal("aibysitter init --packs aspnet-web-api --format claude", api.GetProperty("init").GetString());
        Assert.Equal(pack.Manifest.Targets, api.GetProperty("targets").EnumerateArray().Select(t => t.GetString()));
        var sections = api.GetProperty("sections").EnumerateArray().ToList();
        Assert.Equal(pack.Sections.Select(s => s.Id), sections.Select(s => s.GetProperty("id").GetString()));
        Assert.Equal(SectionScore(pack.Sections[0]).Grade, sections[0].GetProperty("grade").GetString());
    }

    [Fact]
    public async Task LlmsTxt_ListsPacks_AndRegistry()
    {
        var text = await factory.CreateClient().GetStringAsync("/llms.txt");

        Assert.Contains("\n## Rules packs\n", text);
        foreach (var pack in Catalog.All)
        {
            Assert.Contains($"- [{pack.Manifest.Title}](https://aibysitting.net/Packs#{pack.Id}): ", text);
        }

        Assert.Contains("- [Packs registry](https://aibysitting.net/packs/registry.json): JSON, schema version 1", text);
    }
}
