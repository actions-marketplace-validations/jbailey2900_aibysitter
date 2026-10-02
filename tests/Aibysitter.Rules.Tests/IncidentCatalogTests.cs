using System.Net;
using Aibysitter.Web.Content;
using Aibysitter.Web.Incidents;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests;

[Trait("Category", "Catalog")]
public class IncidentCatalogTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Body = "## What happened\n- x\n\n## Impact\n- x\n\n## Detection\nx\n\n## Why review and CI missed it\n- x\n\n## Fix\n- x\n";

    private static string Entry(string frontmatter = "", string body = Body) =>
        "---\nid: 7\ntitle: T\ndate: 2026-10-02\nagent: A\nsubmitter: some-user\ncaught-by: none\n" + frontmatter + "---\n" + body;

    private static IncidentFormatException Invalid(Dictionary<string, string> files) =>
        Assert.Throws<IncidentFormatException>(() => new IncidentCatalog(files));

    [Fact]
    public void Embedded_Incident1()
    {
        var incident = Assert.Single(new IncidentCatalog().All);

        Assert.Equal(1, incident.Id);
        Assert.Equal("linux-ci-green-windows-production-down", incident.Slug);
        Assert.Equal("Linux CI green, Windows production down", incident.Title);
        Assert.Equal(new DateOnly(2026, 10, 2), incident.Date);
        Assert.Equal("Claude Code (Claude cloud sessions)", incident.Agent);
        Assert.Null(incident.Model);
        Assert.Equal("jbailey2900", incident.Submitter);
        Assert.Equal("/Notes/field-note-2", incident.Source);
        Assert.Empty(incident.CaughtBy);
        Assert.Contains("<h2 id=\"reproduced-by\">Reproduced by</h2>", incident.Html);
        Assert.DoesNotContain("submitter:", incident.Html);
    }

    [Fact]
    public void Valid_OptionalFields_CaughtByIds_ReproducedBy_NewestFirst()
    {
        var catalog = new IncidentCatalog(new Dictionary<string, string>
        {
            ["0007-a.md"] = Entry("model: m1\nsource: https://github.com/o/r/issues/1\n").Replace("caught-by: none", "caught-by: P001, R002"),
            ["0008-b.md"] = Entry(body: Body + "\n## Reproduced by\n```\nrun\n```\n").Replace("id: 7", "id: 8"),
        });

        Assert.Equal([8, 7], catalog.All.Select(i => i.Id));
        var a = catalog.Find(7)!;
        Assert.Equal(["P001", "R002"], a.CaughtBy);
        Assert.Equal("m1", a.Model);
        Assert.Equal("https://github.com/o/r/issues/1", a.Source);
        Assert.Null(catalog.Find(8)!.Source);
    }

    [Fact]
    public void Validation_ReportsEveryProblem()
    {
        var ex = Invalid(new()
        {
            ["notes.md"] = Entry(),
            ["0002-no-frontmatter.md"] = Body,
            ["0003-keys.md"] = "---\nid: 4\ndate: 2 Oct\nagent: A\nsubmitter: -bad\ncaught-by: P999, none\nsource: ftp://x\nseverity: high\n---\n" + Body,
            ["0005-sections.md"] = Entry(body: "# Title\n## What happened\n- x\n## Fix\n- x\n").Replace("id: 7", "id: 5"),
            ["0006-empty.md"] = Entry(body: Body.Replace("## Detection\nx\n", "## Detection\n\n")).Replace("id: 7", "id: 6"),
            ["0007-a.md"] = Entry(),
            ["0007-b.md"] = Entry(),
        });

        Assert.Equal(
        [
            "0002-no-frontmatter.md: frontmatter block is missing",
            "0003-keys.md: unknown key \"severity\"",
            "0003-keys.md: \"title\" is required",
            "0003-keys.md: id must match the file number",
            "0003-keys.md: date must be yyyy-MM-dd",
            "0003-keys.md: submitter must be a GitHub username",
            "0003-keys.md: source must be an https URL or a site path",
            "0003-keys.md: caught-by has unknown ID \"P999\"",
            "0003-keys.md: caught-by is \"none\" or a list of IDs, not both",
            "0005-sections.md: no H1; the title comes from the frontmatter",
            "0005-sections.md: sections must be \"What happened\", \"Impact\", \"Detection\", \"Why review and CI missed it\", \"Fix\", then optionally \"Reproduced by\", in that order",
            "0006-empty.md: section \"Detection\" is empty",
            "notes.md: incident files are named NNNN-slug.md",
            "id 7 is used more than once",
        ], ex.Errors);
    }

    [Fact]
    public void FrontmatterBlock_ParsesValues_AndBody()
    {
        var (values, body) = FrontmatterBlock.Parse("---\r\ntitle: A: b\r\nnot a pair\r\n---\r\n## H\n")!.Value;

        Assert.Equal("A: b", values["title"]);
        Assert.Single(values);
        Assert.Equal("## H\n", body);
        Assert.Null(FrontmatterBlock.Parse("## H\n"));
        Assert.Null(FrontmatterBlock.Parse("---\ntitle: x\n"));
    }

    [Fact]
    public async Task IndexPage_ListsIncident_CoverageLine_SubmitLink()
    {
        var html = await factory.CreateClient().GetStringAsync("/Incidents");

        Assert.Contains("<h1>Incidents</h1>", html);
        Assert.Contains("1 incident. Caught by an existing check: 0. Caught by none: 1.", html);
        Assert.Contains("href=\"/Incidents/1\">Linux CI green, Windows production down</a>", html);
        Assert.Contains($"href=\"{IncidentCatalog.SubmitUrl}\">Submit an incident</a>", html);
        Assert.Contains($"href=\"{IncidentCatalog.LicenseUrl}\">CC BY 4.0</a>", html);
    }

    [Fact]
    public async Task EntryPage_Meta_Sections_Credit()
    {
        var html = await factory.CreateClient().GetStringAsync("/Incidents/1");

        Assert.Contains("<h1>Incident 1: Linux CI green, Windows production down</h1>", html);
        Assert.Contains("<dd>Claude Code (Claude cloud sessions)</dd>", html);
        Assert.Contains("<h2 id=\"why-review-and-ci-missed-it\">Why review and CI missed it</h2>", html);
        Assert.Contains("Submitted by <a href=\"https://github.com/jbailey2900\">@jbailey2900</a>", html);
        Assert.Contains("<a href=\"/Notes/field-note-2\">Source</a>", html);
        Assert.Contains("<link rel=\"canonical\" href=\"https://aibysitting.net/Incidents/1\" />", html);
    }

    [Theory]
    [InlineData("/Incidents/2")]
    [InlineData("/Incidents/x")]
    public async Task UnknownIncident_404(string path)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Footer_Privacy_LlmsTxt_LinkIncidents()
    {
        var client = factory.CreateClient();

        Assert.Contains("<a href=\"/Incidents\">Incidents</a>", await client.GetStringAsync("/"));
        Assert.Contains("A published incident shows the submitter's GitHub username", await client.GetStringAsync("/Privacy"));
        Assert.Contains("- [Incidents](https://aibysitting.net/Incidents): ", await client.GetStringAsync("/llms.txt"));
    }

    [Fact]
    public void IncidentHtml_IsCspSafe()
    {
        Assert.All(new IncidentCatalog().All, i =>
        {
            Assert.DoesNotContain("style=", i.Html);
            Assert.DoesNotContain("<script", i.Html);
            Assert.DoesNotMatch(@"\son\w+=", i.Html);
        });
    }
}
