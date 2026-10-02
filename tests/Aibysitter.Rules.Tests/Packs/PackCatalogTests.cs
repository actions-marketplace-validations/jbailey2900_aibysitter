using System.Text.RegularExpressions;
using Aibysitter.Packs;
using Aibysitter.Web.Gallery;

namespace Aibysitter.Rules.Tests.Packs;

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

    /// <summary>Packs were split from gallery entries; sections follow the entry's H2 headings in order, and the intro exists when the entry has one.</summary>
    [Fact]
    public void Packs_MatchTheirGallerySource_Headings()
    {
        var gallery = new GalleryCatalog(Engine);
        foreach (var pack in Catalog.All)
        {
            var source = gallery.Find(pack.Manifest.Source!);
            Assert.True(source is not null, $"{pack.Id}: source {pack.Manifest.Source} not in the gallery");
            var lines = source!.Content.Replace("\r\n", "\n").Split('\n');
            var headings = lines.Where(l => l.StartsWith("## ", StringComparison.Ordinal)).Select(l => l[3..].Trim()).ToList();
            var hasIntro = lines.Skip(1).TakeWhile(l => !l.StartsWith("## ", StringComparison.Ordinal)).Any(l => l.Trim().Length > 0);

            Assert.Equal(headings, pack.Sections.Select(s => s.Heading).ToList());
            Assert.Equal(hasIntro, pack.Intro is not null);
        }
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

    [Fact]
    public void Validation_UnknownManifestKey_AndSchemaVersion()
    {
        Assert.Contains(Invalid(new() { ["demo/pack.json"] = Manifest.Replace("}", ",\"extra\":1}"), ["demo/01-a.md"] = "## A\n- x\n" }).Errors, e => e.StartsWith("demo/pack.json: ", StringComparison.Ordinal));
        Assert.Contains("demo/pack.json: schemaVersion must be 1", Invalid(new() { ["demo/pack.json"] = Manifest.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"), ["demo/01-a.md"] = "## A\n- x\n" }).Errors);
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
