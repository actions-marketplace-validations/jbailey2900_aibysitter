using Aibysitter.Rules.Rules;
using Aibysitter.Web.Gallery;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests;

public class RulesFormatsTests
{
    [Theory]
    [InlineData("CLAUDE.md", RulesFormat.ClaudeMd)]
    [InlineData("docs/AGENTS.md", RulesFormat.AgentsMd)]
    [InlineData(".cursor/rules/storefront.mdc", RulesFormat.CursorMdc)]
    [InlineData("packages/web/.cursor/rules/api/handlers.mdc", RulesFormat.CursorMdc)]
    [InlineData("./.cursorrules", RulesFormat.CursorRules)]
    [InlineData("services/api/GEMINI.md", RulesFormat.GeminiMd)]
    [InlineData("src/CLAUDE.md", RulesFormat.ClaudeMd)]
    [InlineData(".cursorrules", RulesFormat.CursorRules)]
    [InlineData(".github/copilot-instructions.md", RulesFormat.CopilotInstructions)]
    [InlineData("GEMINI.md", RulesFormat.GeminiMd)]
    [InlineData(".windsurfrules", RulesFormat.WindsurfRules)]
    public void FromFileName_Known(string path, RulesFormat format) => Assert.Equal(format, RulesFormats.FromFileName(path));

    [Theory]
    [InlineData("README.md")]
    [InlineData("claude.md")]
    [InlineData(".mdc")]
    [InlineData("storefront.mdc")]
    [InlineData("docs/components/card.mdc")]
    [InlineData("cursor/rules/a.mdc")]
    [InlineData("copilot-instructions.md")]
    [InlineData("docs/.github/copilot-instructions.md")]
    [InlineData("src/.cursorrules")]
    [InlineData("web/.windsurfrules")]
    [InlineData("rules.txt")]
    public void FromFileName_Unknown(string path) => Assert.Null(RulesFormats.FromFileName(path));

    [Fact]
    public void Selectable_StartsWithAuto_ExcludesMarkdown()
    {
        Assert.Equal(RulesFormat.Auto, RulesFormats.Selectable[0]);
        Assert.DoesNotContain(RulesFormat.Markdown, RulesFormats.Selectable);
    }
}

public class FrontmatterTests
{
    [Fact]
    public void Parsed_LinesMarked_KeysAndValuesRead()
    {
        var file = RulesFile.Parse("---\ndescription: \"API rules\"\nglobs: src/**/*.ts, tests/**/*.ts\nalwaysApply: false\n---\n# Rules\n- Use tabs.\n");

        Assert.Equal(5, file.Frontmatter!.EndLine);
        Assert.All(file.Lines.Take(5), l => Assert.True(l.IsFrontmatter));
        Assert.False(file.Lines[5].IsFrontmatter);
        Assert.Equal(new[] { "API rules" }, file.Frontmatter.Values("description"));
        Assert.Equal(new[] { "src/**/*.ts", "tests/**/*.ts" }, file.Frontmatter.Values("globs"));
        Assert.Equal(4, file.Frontmatter.LineOf("alwaysApply"));
        Assert.Equal(RulesFormat.CursorMdc, file.Format);
    }

    [Fact]
    public void ListAndFlowValues()
    {
        var fm = RulesFile.Parse("---\nglobs:\n  - \"*.ts\"\n  - \"*.tsx\"\ntags: [a, b]\n---\n").Frontmatter!;

        Assert.Equal(new[] { "*.ts", "*.tsx" }, fm.Values("globs"));
        Assert.Equal(new[] { "a", "b" }, fm.Values("tags"));
    }

    [Theory]
    [InlineData("# Rules\n---\ndescription: x\n---\n")]
    [InlineData("---\ndescription: x\n- no close\n")]
    public void NotAtTopOrUnclosed_NoFrontmatter(string text)
    {
        var file = RulesFile.Parse(text);

        Assert.Null(file.Frontmatter);
        Assert.Equal(RulesFormat.Markdown, file.Format);
    }

    [Fact]
    public void NonCursorFrontmatter_AutoIsMarkdown() => Assert.Equal(RulesFormat.Markdown, RulesFile.Parse("---\ntitle: x\n---\n").Format);

    [Fact]
    public void ExplicitFormat_Wins() => Assert.Equal(RulesFormat.GeminiMd, RulesFile.Parse("---\nglobs: x\n---\n", RulesFormat.GeminiMd).Format);

    [Fact]
    public void FrontmatterSkippedByProseRules_ButScannedForSecrets()
    {
        var engine = new LintEngine();
        var text = "---\ndescription: You are an expert. Handle errors properly. Try to be brief.\nglobs: x\napi: Password=Hunter2Prod;\nalwaysApply: true\n---\n- Use tabs.\n";

        var ids = engine.Lint(text).Select(f => f.RuleId).ToList();

        Assert.DoesNotContain("R002", ids);
        Assert.DoesNotContain("R007", ids);
        Assert.DoesNotContain("R010", ids);
        Assert.Contains("R009", ids);
    }

    [Fact]
    public void DuplicateFrontmatterAndBodyLine_NotR005() =>
        Assert.DoesNotContain(new LintEngine().Lint("---\ndescription: Run the tests before commit.\n---\n- Run the tests before commit.\n"), f => f.RuleId == "R005");
}

public class FrontmatterFieldsTests
{
    private readonly FrontmatterFields _rule = new();

    private IReadOnlyList<Finding> Lint(string text, RulesFormat format = RulesFormat.Auto) => _rule.Evaluate(RulesFile.Parse(text, format)).ToList();

    [Theory]
    [InlineData("---\ndescription: x\nglobs:\nalwaysApply: true\n---\n- a\n")]
    [InlineData("---\ndescription:\nglobs: src/**\nalwaysApply: false\n---\n- a\n")]
    [InlineData("---\ndescription: Rules for API handlers\nglobs:\nalwaysApply: false\n---\n- a\n")]
    public void Valid_NoFindings(string text) => Assert.Empty(Lint(text));

    [Fact]
    public void NeverApplies_Flagged() =>
        Assert.Contains("never applies", Assert.Single(Lint("---\ndescription:\nglobs:\nalwaysApply: false\n---\n- a\n")).Message);

    [Fact]
    public void MissingAlwaysApply_NoGlobsNoDescription_Flagged() =>
        Assert.Single(Lint("---\nglobs:\n---\n- a\n"));

    [Fact]
    public void UnknownKey_FlaggedAtItsLine()
    {
        var finding = Assert.Single(Lint("---\ndescription: x\nglob: src/**\nalwaysApply: true\n---\n"));

        Assert.Equal(3, finding.Line);
        Assert.Contains("\"glob\"", finding.Message);
    }

    [Fact]
    public void NonBooleanAlwaysApply_Flagged() =>
        Assert.Contains(Lint("---\ndescription: x\nalwaysApply: yes\n---\n"), f => f.Message == "alwaysApply must be true or false.");

    [Fact]
    public void MdcWithoutFrontmatter_Flagged() =>
        Assert.Equal("Cursor rule has no frontmatter.", Assert.Single(Lint("# Rules\n- a\n", RulesFormat.CursorMdc)).Message);

    [Theory]
    [InlineData(RulesFormat.ClaudeMd)]
    [InlineData(RulesFormat.CursorRules)]
    [InlineData(RulesFormat.Markdown)]
    public void OtherFormats_Skipped(RulesFormat format) => Assert.Empty(Lint("---\nfoo: bar\n---\n", format));
}

public class FormatWebTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task LintPage_HasFormatDropdown_AutoSelected()
    {
        var html = await factory.CreateClient().GetStringAsync("/Lint");

        Assert.Contains("<select", html);
        Assert.Contains("<option selected=\"selected\" value=\"Auto\">Auto-detect</option>", html);
        Assert.Contains("<option value=\"CursorMdc\">Cursor rule (.mdc)</option>", html);
        Assert.DoesNotContain("value=\"Markdown\"", html);
    }

    [Fact]
    public async Task Post_Auto_ShowsDetectedFormat_AndRunsR015()
    {
        var html = await (await LintClient.PostAsync(factory.CreateClient(), "---\ndescription:\nglobs:\nalwaysApply: false\n---\n- Use tabs.\n")).Content.ReadAsStringAsync();

        Assert.Contains("Linted as Cursor rule (.mdc) (detected).", html);
        Assert.Contains("<a href=\"/Notes/R015\">R015</a>", html);
    }

    [Fact]
    public async Task GalleryPrefill_SelectsEntryFormat()
    {
        var html = await factory.CreateClient().GetStringAsync("/Lint?gallery=aspnet-web-api-copilot");

        Assert.Contains("<option selected=\"selected\" value=\"CopilotInstructions\">", html);
    }

    [Theory]
    [InlineData("python-fastapi-cursorrules", ".cursorrules", "text/plain")]
    [InlineData("nextjs-typescript-cursor", "storefront.mdc", "text/markdown")]
    [InlineData("minimal-starter-windsurf", ".windsurfrules", "text/plain")]
    public async Task Download_DotAndMdcFiles(string id, string file, string mediaType)
    {
        var response = await factory.CreateClient().GetAsync($"/gallery/{id}/{file}");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task EntryPage_ShowsFormatAndInstallPath()
    {
        var html = await factory.CreateClient().GetStringAsync("/Gallery/nextjs-typescript-cursor");

        Assert.Contains("<dd>Cursor rule (.mdc)</dd>", html);
        Assert.Contains("<code>.cursor/rules/storefront.mdc</code>", html);
    }

    [Fact]
    public async Task Registry_HasFormatAndInstallPath()
    {
        var json = await factory.CreateClient().GetStringAsync("/registry.json");

        Assert.Contains("\"format\":\"Copilot instructions\"", json);
        Assert.Contains("\"installPath\":\".github/copilot-instructions.md\"", json);
    }
}
