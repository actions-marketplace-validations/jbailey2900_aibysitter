using System.Text.RegularExpressions;
using Aibysitter.Packs;
using Aibysitter.Web.Gallery;

namespace Aibysitter.Rules.Tests.Packs;

[Trait("Category", "Catalog")]
public class PackCatalogTests
{
    private static readonly PackCatalog Catalog = new();
    private static readonly LintEngine Engine = new();

    private const string Manifest = """{"schemaVersion":1,"id":"demo","title":"Demo","description":"d","tags":["t"],"targets":["ClaudeMd"],"license":"CC0-1.0"}""";

    [Fact]
    public void Embedded_HasTheEightPacks()
    {
        Assert.Equal(
            ["aspnet-web-api", "dotnet-razor-pages", "go-http-service", "monorepo", "nextjs-typescript", "python-fastapi", "starter", "winforms-devexpress"],
            Catalog.All.Select(p => p.Id));
    }

    [Fact]
    public void EverySection_LintsAlone_WithNoErrorFinding()
    {
        var errors = Catalog.All.SelectMany(p => p.Sections.Select(s => (p, s)))
            .SelectMany(x => Engine.Lint(x.s.Markdown, RulesFormat.Markdown)
                .Where(f => Engine.SeverityOf(f.RuleId) == Severity.Error)
                .Select(f => $"{x.p.Id}/{x.s.Id}:{f.Line} {f.RuleId} {f.Message}"))
            .ToList();

        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Fact]
    public void EveryPack_ComposedAlone_LintsWithNoErrorFinding_InEveryTarget()
    {
        var errors = new List<string>();
        foreach (var pack in Catalog.All)
        {
            foreach (var target in pack.Manifest.Targets.Select(Enum.Parse<RulesFormat>))
            {
                var result = Engine.Analyze(PackComposer.Compose([pack], target), target);
                errors.AddRange(result.Findings.Where(f => Engine.SeverityOf(f.RuleId) == Severity.Error || f.RuleId is "R015" or "R016")
                    .Select(f => $"{pack.Id} as {target}:{f.Line} {f.RuleId} {f.Message}"));
            }
        }

        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Fact]
    public void EveryPack_IsUsedByAGalleryEntry()
    {
        var used = new GalleryCatalog(Engine).All.Select(e => e.PackId).ToHashSet();

        Assert.All(Catalog.All, p => Assert.Contains(p.Id, used));
    }

    [Fact]
    public void Starter_IsStandalone_OthersAreNot()
    {
        Assert.Equal(["starter"], Catalog.All.Where(p => p.Manifest.Standalone).Select(p => p.Id));
    }

    [Fact]
    public void Monorepo_Intro_UsesRulesFileToken()
    {
        Assert.Contains("A package can have its own `{{rules-file}}`.", Catalog.Find("monorepo")!.Intro);
    }

    [Fact]
    public void Packs_KeepNoGalleryProjectNames()
    {
        var names = new Regex(@"Inventory|Orders\.|OrdersDbContext|Notify|Storefront|Billing|billing|Payroll|Acme|acme|StockLedger");
        var hits = Catalog.All.SelectMany(p => p.Sections.Select(s => (p.Id, s.Markdown)).Append((p.Id, p.Intro ?? "")))
            .Where(x => names.IsMatch(x.Item2)).Select(x => x.Id).Distinct().ToList();

        Assert.Empty(hits);
    }

    [Fact]
    public void SectionIds_AndOrder_FromFileNames()
    {
        var api = Catalog.Find("aspnet-web-api")!;

        Assert.Equal(["commands", "endpoints", "errors", "data", "authorization", "tests", "pull-requests"], api.Sections.Select(s => s.Id));
        Assert.Equal("Pull requests", api.Sections[^1].Heading);
        Assert.StartsWith("ASP.NET Core minimal APIs on .NET 10.", api.Intro);
    }

    private static PackFormatException Invalid(Dictionary<string, string> files) =>
        Assert.Throws<PackFormatException>(() => new PackCatalog(files));

    [Fact]
    public void Validation_ReportsEveryProblem()
    {
        var ex = Invalid(new()
        {
            ["demo/pack.json"] = Manifest.Replace("\"ClaudeMd\"", "\"ClaudeMd\",\"Markdown\",\"Word\"").Replace("\"id\":\"demo\"", "\"id\":\"Demo\""),
            ["demo/01-a.md"] = "## A\n- x\n",
            ["demo/02-b.md"] = "# B\n- y\n",
            ["demo/03-c.md"] = "## a\n- dup heading\n",
            ["demo/04-d.md"] = "## D\n",
            ["demo/05-e.md"] = "## E\n- x\n## F\n- y\n",
            ["demo/notes.md"] = "## G\n- z\n",
            ["demo/intro.md"] = "# Heading in intro\n",
            ["other/01-a.md"] = "## A\n- x\n",
        });

        Assert.Equal(
        [
            "demo/pack.json: id \"Demo\" must match the folder name",
            "demo/pack.json: id must be lowercase kebab-case",
            "demo/pack.json: unknown target \"Markdown\"",
            "demo/pack.json: unknown target \"Word\"",
            "demo/02-b.md: must start with one H2 heading",
            "demo/04-d.md: heading has no content",
            "demo/05-e.md: only one H1 or H2 heading is allowed",
            "demo/notes.md: section files are named NN-name.md",
            "demo: heading \"A\" appears more than once",
            "demo/intro.md: must be non-empty text without headings",
            "other: pack.json is missing",
        ], ex.Errors);
    }

    [Theory]
    [InlineData("- Keep `AGENTS.md` short.", "names a rules file (AGENTS.md)")]
    [InlineData("- See CLAUDE.md.", "names a rules file (CLAUDE.md)")]
    [InlineData("- Edit .cursorrules only.", "names a rules file (.cursorrules)")]
    [InlineData("- Rules live in `.cursor/rules/app.mdc`.", "names a rules file (app.mdc)")]
    [InlineData("- Use {{project}} here.", "unknown token {{project}}")]
    public void Validation_RulesFileNames_NeedTheToken(string line, string error)
    {
        var ex = Invalid(new() { ["demo/pack.json"] = Manifest, ["demo/01-a.md"] = "## A\n" + line + "\n" });

        Assert.Contains(ex.Errors, e => e.StartsWith("demo/01-a.md: " + error, StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_TokenAndLookalikes_AreAccepted()
    {
        var catalog = new PackCatalog(new Dictionary<string, string>
        {
            ["demo/pack.json"] = Manifest,
            ["demo/01-a.md"] = "## A\n- Keep `{{rules-file}}` short.\n- Edit MYCLAUDE.md.notes and README.md.\n",
        });

        Assert.Single(catalog.All);
    }

    [Fact]
    public void Validation_UnknownManifestKey_AndSchemaVersion()
    {
        Assert.Contains(Invalid(new() { ["demo/pack.json"] = Manifest.Replace("}", ",\"extra\":1}"), ["demo/01-a.md"] = "## A\n- x\n" }).Errors, e => e.StartsWith("demo/pack.json: ", StringComparison.Ordinal));
        Assert.Contains("demo/pack.json: schemaVersion must be 1", Invalid(new() { ["demo/pack.json"] = Manifest.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"), ["demo/01-a.md"] = "## A\n- x\n" }).Errors);
    }

    [Fact]
    public void WindowsSeparators_LoadTheSamePacks()
    {
        var catalog = new PackCatalog(new Dictionary<string, string>
        {
            ["demo\\pack.json"] = Manifest,
            ["demo\\intro.md"] = "Intro.",
            ["demo\\01-a.md"] = "## A\n- x\n",
        });

        var pack = catalog.All.Single();
        Assert.Equal("demo", pack.Id);
        Assert.Equal("Intro.", pack.Intro);
        Assert.Equal("a", pack.Sections.Single().Id);
    }

    [Fact]
    public void Validation_KeysWithoutFolderOrFile_AndDuplicatesAfterNormalizing()
    {
        var ex = Invalid(new()
        {
            ["pack.json"] = Manifest,
            ["demo/"] = "",
            ["/01-a.md"] = "## A\n- x\n",
            ["demo/pack.json"] = Manifest,
            ["demo\\pack.json"] = Manifest,
            ["demo/01-a.md"] = "## A\n- x\n",
        });

        Assert.Equal(
        [
            "/01-a.md: path must be <id>/<file>",
            "demo/: path must be <id>/<file>",
            "demo/pack.json: appears more than once",
            "pack.json: path must be <id>/<file>",
        ], ex.Errors);
    }

    [Fact]
    public void EmbeddedFiles_Select_NormalizesSeparators_AndKeepsTheResourceName()
    {
        string[] names = ["packs/starter\\pack.json", "packs/starter/01-a.md", "gallery/x\\CLAUDE.md", "Aibysitter.Web.Samples.sample-rules.md"];

        Assert.Equal(
            [("starter/pack.json", "packs/starter\\pack.json"), ("starter/01-a.md", "packs/starter/01-a.md")],
            EmbeddedFiles.Select(names, "packs/"));
    }

    [Fact]
    public void EmbeddedContent_HasLfLineEndings()
    {
        var withCr = Catalog.All.SelectMany(p => p.Sections.Select(s => (Name: $"packs/{p.Id}/{s.Id}", Text: s.Markdown)).Append((Name: $"packs/{p.Id}/intro", Text: p.Intro ?? "")))
            .Concat(new GalleryCatalog(Engine).All.Select(e => (Name: $"gallery/{e.Id}", Text: e.Content)))
            .Concat(new Aibysitter.Web.Notes.NoteCatalog().All.Select(n => (Name: $"notes/{n.Slug}", Text: n.Html)))
            .Where(x => x.Text.Contains('\r'))
            .Select(x => x.Name);

        Assert.Empty(withCr);
    }

    [Fact]
    public void Validation_HeadingsInsideCodeFences_AreFine_CrlfNormalized()
    {
        var catalog = new PackCatalog(new Dictionary<string, string>
        {
            ["demo/pack.json"] = Manifest,
            ["demo/01-a.md"] = "## A\r\n```bash\r\n# comment\r\nmake\r\n```\r\n",
        });

        Assert.Equal("## A\n```bash\n# comment\nmake\n```", catalog.All.Single().Sections.Single().Markdown);
    }
}
