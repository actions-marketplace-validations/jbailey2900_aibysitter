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
    [InlineData("R005", "- Run tests before commit.")]
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
    public void Duplicates_InCodeOrUnderOtherParents_Untouched()
    {
        const string text = "# Rules\n## Api\n### Testing rules here\n- x\n## Web\n### Testing rules here\n- y\n```\n- Run tests before commit.\n- Run tests before commit.\n```\n";

        Assert.Equal(text, Fix(text).Text);
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
    public void UnclosedFence_AndDuplicateBeforeIt_BothFixed_FenceFirst()
    {
        var result = Fix("# Rules\n- Run tests before commit.\n- Run tests before commit.\n```\nmake\n- Run tests before commit.\n");

        Assert.Equal("# Rules\n- Run tests before commit.\n```\nmake\n- Run tests before commit.\n```\n", result.Text);
        Assert.Equal(["R012", "R005"], result.Fixed.Select(f => f.RuleId));
    }

    [Fact]
    public void SuppressedAndDisabled_Untouched()
    {
        const string suppressed = "# Rules\n- Run tests before commit.\n<!-- aibysitter-disable-next-line R005 -->\n- Run tests before commit.\n";
        Assert.Equal(suppressed, Fix(suppressed).Text);

        const string duplicate = "# Rules\n- Run tests before commit.\n- Run tests before commit.\n";
        Assert.Equal(duplicate, Fix(duplicate, "R005").Text);
    }

    [Fact]
    public void Preserves_Crlf_Bom_AndMissingTrailingNewline()
    {
        Assert.Equal("﻿# Rules\r\n- Run tests before commit.\r\n", Fix("﻿# Rules\r\n- Run tests before commit.\r\n- run tests before commit.\r\n").Text);
        Assert.Equal("# Rules\n- Run tests before commit.", Fix("# Rules\n- Run tests before commit.\n- Run tests before commit.").Text);
    }

    [Fact]
    public void EveryGalleryEntryAndPack_AlreadyClean()
    {
        var texts = new GalleryCatalog(Engine).All.Select(e => (e.Id, e.Content, e.Format))
            .Concat(new PackCatalog().All.Select(p => (p.Id, PackComposer.Compose([p], RulesFormat.ClaudeMd), RulesFormat.ClaudeMd)));

        Assert.All(texts, t => Assert.Equal(t.Item2, RulesFileFixer.Fix(Engine, t.Item2, t.Item3, []).Text));
    }
}
