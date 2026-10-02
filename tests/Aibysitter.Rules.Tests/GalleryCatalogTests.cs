using Aibysitter.Web.Gallery;

namespace Aibysitter.Rules.Tests;

[Trait("Category", "Catalog")]
public class GalleryCatalogTests
{
    private static readonly GalleryCatalog Catalog = new(new LintEngine());

    [Fact]
    public void LoadsThirteenEntries_WithUniqueIds()
    {
        Assert.Equal(13, Catalog.All.Count);
        Assert.Equal(Catalog.All.Count, Catalog.All.Select(e => e.Id).Distinct().Count());
    }

    [Fact]
    public void GalleryLicenseFile_IsNotEmbedded()
    {
        var names = typeof(GalleryCatalog).Assembly.GetManifestResourceNames().Select(n => n.Replace('\\', '/'));

        Assert.DoesNotContain(names, n => n.StartsWith(GalleryCatalog.ResourcePrefix + "LICENSE", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryEntry_IsComposedFromAPack()
    {
        var packs = new Aibysitter.Packs.PackCatalog();
        Assert.All(Catalog.All, e =>
        {
            Assert.NotNull(e.PackId);
            var pack = packs.Find(e.PackId!)!;
            var title = e.Content.Split('\n').First(l => l.StartsWith("# ", StringComparison.Ordinal))[2..];
            Assert.Equal(Aibysitter.Packs.PackComposer.Compose([pack], e.Format, title, e.InstallPath), e.Content);
        });
    }

    [Fact]
    public void UnknownPack_OrPackWithoutTheFormat_FailsToLoad()
    {
        var none = new GalleryCatalog(new LintEngine(), new Aibysitter.Packs.PackCatalog(new Dictionary<string, string>()));
        var claudeOnly = new GalleryCatalog(new LintEngine(), new Aibysitter.Packs.PackCatalog(new Dictionary<string, string>
        {
            ["aspnet-web-api/pack.json"] = """{"schemaVersion":1,"id":"aspnet-web-api","title":"t","description":"d","tags":["t"],"targets":["AgentsMd"],"license":"CC0-1.0"}""",
            ["aspnet-web-api/01-a.md"] = "## A\n- x\n",
        }));

        Assert.Contains("pack 'aspnet-web-api' not found", Assert.Throws<InvalidOperationException>(() => none.All).Message);
        Assert.Contains("pack 'aspnet-web-api' does not target CopilotInstructions", Assert.Throws<InvalidOperationException>(() => claudeOnly.All).Message);
    }

    [Fact]
    public void GalleryFolders_HoldOnlyEntryJson()
    {
        var names = typeof(GalleryCatalog).Assembly.GetManifestResourceNames().Select(n => n.Replace('\\', '/'))
            .Where(n => n.StartsWith(GalleryCatalog.ResourcePrefix, StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(names);
        Assert.All(names, n => Assert.EndsWith("/entry.json", n));
    }

    [Theory]
    [InlineData("monorepo-root", "A package can have its own `AGENTS.md`.")]
    [InlineData("nextjs-typescript-cursor", "---\ndescription: Next.js with TypeScript\nalwaysApply: true\n---\n\n# Next.js with TypeScript\n")]
    [InlineData("minimal-starter-windsurf", "# Project rules\n\nFill in the bracketed parts.")]
    [InlineData("aspnet-web-api-copilot", "# ASP.NET Core web API\n\nASP.NET Core minimal APIs on .NET 10.")]
    public void ComposedContent_ByFormatAndTitle(string id, string expected)
    {
        Assert.Contains(expected, Catalog.Find(id)!.Content);
    }

    [Fact]
    public void EveryEntry_HasCompleteMetadata_AndCc0License()
    {
        Assert.All(Catalog.All, e =>
        {
            Assert.Equal(e.Format, RulesFormats.FromFileName(e.InstallPath));
            Assert.Equal("CC0-1.0", e.License);
            Assert.NotEmpty(e.Tags);
            Assert.False(string.IsNullOrWhiteSpace(e.Description));
            Assert.StartsWith("# ", e.Content[(e.Content.StartsWith("---\n", StringComparison.Ordinal) ? e.Content.IndexOf("\n---\n", 4, StringComparison.Ordinal) + 5 : 0)..].TrimStart('\n'));
        });
    }

    [Fact]
    public void EveryEntry_ScoresAtLeastB_AndStaysUnder200Lines()
    {
        Assert.All(Catalog.All, e =>
        {
            Assert.True(e.Score.Value >= 80, $"{e.Id} scored {e.Score.Value} ({e.Score.Grade})");
            Assert.True(e.Lines.Count <= 200, $"{e.Id} has {e.Lines.Count} lines");
        });
    }

    [Fact]
    public void CategoriesAndTags_AreDistinctAndSorted()
    {
        Assert.Equal(new[] { ".NET", "General", "Go", "JavaScript", "Python" }, Catalog.Categories);
        Assert.Equal(Catalog.Tags.Order(StringComparer.OrdinalIgnoreCase), Catalog.Tags);
        Assert.Contains("C#", Catalog.Tags);
    }

    [Theory]
    [InlineData("go-http-service")]
    [InlineData("GO-HTTP-SERVICE")]
    public void Find_IsCaseInsensitive(string id)
    {
        Assert.Equal("Go HTTP service", Catalog.Find(id)?.Name);
    }

    [Fact]
    public void Find_Unknown_ReturnsNull()
    {
        Assert.Null(Catalog.Find("nope"));
    }

    [Fact]
    public void DownloadPath_UsesIdAndFileName()
    {
        Assert.Equal("/gallery/aspnet-web-api/AGENTS.md", Catalog.Find("aspnet-web-api")!.DownloadPath);
    }

    [Theory]
    [InlineData("nextjs-typescript-cursor", "nextjs-typescript", RulesFormat.CursorMdc, ".cursor/rules/nextjs.mdc")]
    [InlineData("python-fastapi-cursorrules", "python-fastapi", RulesFormat.CursorRules, ".cursorrules")]
    [InlineData("aspnet-web-api-copilot", "aspnet-web-api", RulesFormat.CopilotInstructions, ".github/copilot-instructions.md")]
    [InlineData("go-http-service-gemini", "go-http-service", RulesFormat.GeminiMd, "GEMINI.md")]
    [InlineData("minimal-starter-windsurf", "minimal-starter", RulesFormat.WindsurfRules, ".windsurfrules")]
    public void FormatVariant_SameBodyAsSource_FormatFromFileName(string id, string sourceId, RulesFormat format, string installPath)
    {
        var variant = Catalog.Find(id)!;
        var source = Catalog.Find(sourceId)!;

        Assert.Equal(format, variant.Format);
        Assert.Equal(installPath, variant.InstallPath);
        Assert.Equal(source.Category, variant.Category);
        Assert.EndsWith(source.Content, variant.Content);
        Assert.Equal(100, variant.Score.Value);
    }
}
