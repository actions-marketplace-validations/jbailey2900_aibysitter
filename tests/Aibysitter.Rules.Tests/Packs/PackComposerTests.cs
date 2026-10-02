using Aibysitter.Packs;

namespace Aibysitter.Rules.Tests.Packs;

[Trait("Category", "Catalog")]
public class PackComposerTests
{
    private static readonly PackCatalog Catalog = new();

    private static Pack Make(string id, string? intro, params string[] sections) =>
        new(new PackManifest(1, id, id, "d", ["t"], ["ClaudeMd"], "CC0-1.0"), intro,
            sections.Select((s, i) => new PackSection($"s{i}", s.Split('\n')[0][3..], s)).ToList());

    [Fact]
    public void SinglePack_TitleIntroSections_LfTrailingNewline()
    {
        var text = PackComposer.Compose([Make("a", "Intro line.", "## Commands\n- Build: `make`", "## Tests\n\nRun them.\n")], RulesFormat.ClaudeMd);

        Assert.Equal("# Project rules\n\nIntro line.\n\n## Commands\n- Build: `make`\n\n## Tests\nRun them.\n", text);
    }

    [Fact]
    public void TwoPacks_NoIntro_MergedHeading_DedupedListItems()
    {
        var a = Make("a", "A intro.", "## Commands\n- Build: `make`\n- Test: `make test`", "## Style\n- Tabs.");
        var b = Make("b", "B intro.", "## Tests\n- One per bug.", "## commands\n- Test: `make test`\n- Lint: `make lint`");

        var text = PackComposer.Compose([a, b], RulesFormat.AgentsMd, "  Shop  ");

        Assert.Equal(
            "# Shop\n\n## Commands\n- Build: `make`\n- Test: `make test`\n- Lint: `make lint`\n\n## Style\n- Tabs.\n\n## Tests\n- One per bug.\n",
            text);
    }

    [Fact]
    public void Merge_NonListContent_SeparatedByBlankLine_AllDuplicatesDropped_SkipsEmptyAddition()
    {
        var a = Make("a", null, "## Notes\nParagraph one.");
        var b = Make("b", null, "## Notes\nParagraph two.");
        var c = Make("c", null, "## Notes\nParagraph two.");
        var d = Make("d", null, "## List\n- x");
        var e = Make("e", null, "## List\n- x");

        Assert.Equal("# Project rules\n\n## Notes\nParagraph one.\n\nParagraph two.\n\nParagraph two.\n", PackComposer.Compose([a, b, c], RulesFormat.ClaudeMd));
        Assert.Equal("# Project rules\n\n## List\n- x\n", PackComposer.Compose([d, e], RulesFormat.ClaudeMd));
    }

    [Fact]
    public void CursorMdc_HasFrontmatter_ThatPassesR015AndR016()
    {
        var text = PackComposer.Compose([Catalog.Find("nextjs-typescript")!], RulesFormat.CursorMdc, "Storefront rules");
        var engine = new LintEngine();

        Assert.StartsWith("---\ndescription: Storefront rules\nalwaysApply: true\n---\n\n# Storefront rules\n", text);
        Assert.DoesNotContain(engine.Lint(text, RulesFormat.CursorMdc), f => f.RuleId is "R015" or "R016");
        Assert.Equal(RulesFormat.CursorMdc, engine.Analyze(text).Format);
    }

    [Theory]
    [InlineData(RulesFormat.ClaudeMd, "CLAUDE.md")]
    [InlineData(RulesFormat.AgentsMd, "AGENTS.md")]
    [InlineData(RulesFormat.CopilotInstructions, ".github/copilot-instructions.md")]
    [InlineData(RulesFormat.CursorMdc, ".cursor/rules/monorepo.mdc")]
    [InlineData(RulesFormat.WindsurfRules, ".windsurfrules")]
    public void RulesFileToken_IsTheOutputPath(RulesFormat format, string path)
    {
        var text = PackComposer.Compose([Catalog.Find("monorepo")!], format);

        Assert.Contains($"A package can have its own `{path}`.", text);
        Assert.DoesNotContain("{{", text);
    }

    [Fact]
    public void RulesFileToken_UsesExplicitPath_InSectionsToo()
    {
        var pack = Make("a", "Intro names `{{rules-file}}`.", "## Notes\n- Keep `{{rules-file}}` under 200 lines.");

        Assert.Equal(
            "# Project rules\n\nIntro names `docs/AGENTS.md`.\n\n## Notes\n- Keep `docs/AGENTS.md` under 200 lines.\n",
            PackComposer.Compose([pack], RulesFormat.AgentsMd, rulesFile: "docs/AGENTS.md"));
    }

    [Fact]
    public void StandalonePack_CannotBeCombined()
    {
        var starter = Catalog.Find("starter")!;

        Assert.Equal([starter], PackComposer.StandaloneConflicts([Catalog.Find("monorepo")!, starter]));
        Assert.Empty(PackComposer.StandaloneConflicts([starter]));
        var ex = Assert.Throws<ArgumentException>(() => PackComposer.Compose([starter, Catalog.Find("monorepo")!], RulesFormat.ClaudeMd));
        Assert.StartsWith("starter is used on its own only.", ex.Message);
        Assert.StartsWith("# Project rules\n\nFill in the bracketed parts.", PackComposer.Compose([starter], RulesFormat.ClaudeMd));
    }

    [Theory]
    [InlineData("claude", RulesFormat.ClaudeMd, "CLAUDE.md")]
    [InlineData("agents", RulesFormat.AgentsMd, "AGENTS.md")]
    [InlineData("gemini", RulesFormat.GeminiMd, "GEMINI.md")]
    [InlineData("copilot", RulesFormat.CopilotInstructions, ".github/copilot-instructions.md")]
    [InlineData("cursor", RulesFormat.CursorMdc, ".cursor/rules/starter.mdc")]
    [InlineData("cursorrules", RulesFormat.CursorRules, ".cursorrules")]
    [InlineData("WINDSURF", RulesFormat.WindsurfRules, ".windsurfrules")]
    [InlineData("ClaudeMd", RulesFormat.ClaudeMd, "CLAUDE.md")]
    [InlineData("cursormdc", RulesFormat.CursorMdc, ".cursor/rules/starter.mdc")]
    public void Formats_ShortAndFullNames_DefaultPaths(string name, RulesFormat format, string path)
    {
        Assert.True(PackComposer.TryParseFormat(name, out var parsed));
        Assert.Equal(format, parsed);
        Assert.Equal(path, PackComposer.DefaultPath(format, [Catalog.Find("starter")!]));
    }

    [Theory]
    [InlineData("Auto")]
    [InlineData("Markdown")]
    [InlineData("word")]
    [InlineData("1")]
    public void Formats_RejectNonOutputs(string name)
    {
        Assert.False(PackComposer.TryParseFormat(name, out _));
    }

    [Fact]
    public void CursorMdc_TwoPacks_DefaultFileName()
    {
        Assert.Equal(".cursor/rules/project-rules.mdc", PackComposer.DefaultPath(RulesFormat.CursorMdc, [Catalog.Find("go-http-service")!, Catalog.Find("monorepo")!]));
    }

    [Fact]
    public void RealPacks_Composed_Together_HaveOneSectionPerHeading()
    {
        var text = PackComposer.Compose([Catalog.Find("dotnet-razor-pages")!, Catalog.Find("python-fastapi")!], RulesFormat.ClaudeMd);
        var headings = text.Split('\n').Where(l => l.StartsWith("## ", StringComparison.Ordinal)).ToList();

        Assert.Equal(headings.Count, headings.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("## Done", headings);
        Assert.Contains("- `ruff check`, `ruff format --check`, `mypy`, and `pytest` all pass.", text);
        Assert.DoesNotContain("ASP.NET Core Razor Pages on .NET 10.", text);
    }
}
