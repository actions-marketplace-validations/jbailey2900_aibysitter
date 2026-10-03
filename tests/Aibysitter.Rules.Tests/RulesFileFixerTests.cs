using Aibysitter.Packs;
using Aibysitter.Web.Gallery;

namespace Aibysitter.Rules.Tests;

public class RulesFileFixerTests
{
    private static readonly LintEngine Engine = new();

    private static FixResult Fix(string text, params string[] disabled) => RulesFileFixer.Fix(Engine, text, RulesFormat.ClaudeMd, disabled);

    private static IEnumerable<string> Fixable(string text) =>
        Engine.Lint(text, RulesFormat.ClaudeMd).Select(f => f.RuleId).Where(RulesFileFixer.FixableRules.Contains);

    [Theory]
    [InlineData("R011", "# Rules\n## Style\n- Use tabs.")]
    [InlineData("R012", "Run:\n```\ndotnet test\n- Use tabs.\n```")]
    public void RuleDocBadExample_Fixed(string id, string expected)
    {
        var result = Fix(RuleDocs.Find(id)!.BadExample);

        Assert.Equal(expected, result.Text);
        Assert.Equal([id], result.Fixed.Select(f => f.RuleId));
        Assert.Empty(Fixable(result.Text));
    }

    [Fact]
    public void DuplicateLines_NotFixed()
    {
        const string text = "# Rules\n- Run tests before commit.\n- Run tests before commit.\n";

        Assert.Contains("R005", Engine.Lint(text, RulesFormat.ClaudeMd).Select(f => f.RuleId));
        Assert.Equal(text, Fix(text).Text);
        Assert.DoesNotContain("R005", RulesFileFixer.FixableRules);
    }

    [Fact]
    public void NestedEmptySections_RemovedOverPasses()
    {
        var result = Fix("# Rules\n## Parent\n### Child\n\n## Style\n- Use tabs.\n");

        Assert.Equal("# Rules\n## Style\n- Use tabs.\n", result.Text);
        Assert.Equal(["R011", "R011"], result.Fixed.Select(f => f.RuleId));
    }

    [Fact]
    public void Fence_ClosedAtEndOfFile_MatchingCharAndLength()
    {
        Assert.Equal("Run:\n~~~~ bash\nmake\n\n- Use tabs.\n~~~~\n", Fix("Run:\n~~~~ bash\nmake\n\n- Use tabs.\n").Text);
        Assert.Equal("Run:\n```\nmake\n```", Fix("Run:\n```\nmake").Text);
    }

    [Fact]
    public void UnclosedFence_AndEmptySectionBeforeIt_BothFixed_FenceFirst()
    {
        var result = Fix("# Rules\n## Empty\n## Build\n```\nmake\n");

        Assert.Equal("# Rules\n## Build\n```\nmake\n```\n", result.Text);
        Assert.Equal(["R012", "R011"], result.Fixed.Select(f => f.RuleId));
    }

    [Fact]
    public void SuppressedAndDisabled_Untouched()
    {
        const string suppressed = "# Rules\n<!-- aibysitter-disable-next-line R011 -->\n## Empty\n## Style\n- Use tabs.\n";
        Assert.Equal(suppressed, Fix(suppressed).Text);

        const string empty = "# Rules\n## Empty\n## Style\n- Use tabs.\n";
        Assert.Equal(empty, Fix(empty, "R011").Text);
    }

    [Fact]
    public void Preserves_Crlf_Bom_AndMissingTrailingNewline()
    {
        Assert.Equal("﻿# Rules\r\n## Style\r\n- Use tabs.\r\n", Fix("﻿# Rules\r\n## Empty\r\n## Style\r\n- Use tabs.\r\n").Text);
        Assert.Equal("# Rules\n## Style\n- Use tabs.", Fix("# Rules\n## Empty\n## Style\n- Use tabs.").Text);
    }

    [Fact]
    public void EveryGalleryEntryAndPack_AlreadyClean()
    {
        var texts = new GalleryCatalog(Engine).All.Select(e => (e.Id, e.Content, e.Format))
            .Concat(new PackCatalog().All.Select(p => (p.Id, PackComposer.Compose([p], RulesFormat.ClaudeMd), RulesFormat.ClaudeMd)));

        Assert.All(texts, t => Assert.Equal(t.Item2, RulesFileFixer.Fix(Engine, t.Item2, t.Item3, []).Text));
    }
}
