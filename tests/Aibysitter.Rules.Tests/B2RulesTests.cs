using Aibysitter.Rules.Rules;

namespace Aibysitter.Rules.Tests;

public class PersonaPreambleTests
{
    private readonly PersonaPreamble _rule = new();

    [Theory]
    [InlineData("You are a senior Cal.diy engineer working in a monorepo.")]
    [InlineData("You're an expert in the following areas:")]
    [InlineData("You are an experienced developer working on this project.")]
    [InlineData("Act as a world-class TypeScript engineer.")]
    [InlineData("- Pretend to be a principal architect.")]
    [InlineData("# You are a 10x engineer")]
    public void Flags(string line) => Assert.Single(_rule.Evaluate(RulesFile.Parse(line)));

    [Theory]
    [InlineData("You are working in a Yarn monorepo.")]
    [InlineData("You are the only maintainer of the deploy script.")]
    [InlineData("Ask a senior engineer before changing the schema.")]
    [InlineData("- Write \"You are an expert\" prompts only in `prompts/`.")]
    [InlineData("- The prompt template starts with `You are an expert`.")]
    [InlineData("| Role | You are a senior reviewer |")]
    public void DoesNotFlag(string line) => Assert.Empty(_rule.Evaluate(RulesFile.Parse(line)));

    [Fact]
    public void CodeBlock_Skipped() => Assert.Empty(_rule.Evaluate(RulesFile.Parse("```\nYou are an expert.\n```\n")));
}

public class EmptySectionsTests
{
    private readonly EmptySections _rule = new();

    private IReadOnlyList<Finding> Lint(string text) => _rule.Evaluate(RulesFile.Parse(text)).ToList();

    [Fact]
    public void SiblingHeadingWithNothingBetween_Flagged()
    {
        var finding = Assert.Single(Lint("# Rules\n- x\n## Testing\n\n## Style\n- Use tabs.\n"));

        Assert.Equal(3, finding.Line);
        Assert.Equal("Section \"Testing\" has no content.", finding.Message);
    }

    [Fact]
    public void HeadingAtEndOfFile_Flagged() => Assert.Equal(3, Assert.Single(Lint("# Rules\n- x\n## Later\n\n")).Line);

    [Fact]
    public void ParentWithOnlySubsections_NotFlagged() => Assert.Empty(Lint("# Rules\n- x\n## Code\n### Style\n- Use tabs.\n"));

    [Fact]
    public void TitleH1FollowedByH1_NotFlagged() => Assert.Empty(Lint("# Project Guide\n\n# Skills\n- x\n"));

    [Fact]
    public void LaterEmptyH1_Flagged() => Assert.Single(Lint("# Guide\n- x\n# Empty\n# Next\n- y\n"));

    [Fact]
    public void CodeBlockCountsAsContent() => Assert.Empty(Lint("# Rules\n- x\n## Build\n```\ndotnet build\n```\n## Next\n- y\n"));

    [Fact]
    public void CommentsOnly_CountAsEmpty() => Assert.Single(Lint("# Rules\n- x\n## Build\n<!-- later -->\n## Next\n- y\n"));
}

public class ProseParagraphTests
{
    private readonly ProseParagraph _rule = new();

    /// <summary>n words, opening with "Always" so the paragraph contains an instruction.</summary>
    private static string Words(int n) => string.Join(" ", Enumerable.Range(1, n).Select(i => i == 1 ? "Always" : $"word{i}"));

    [Fact]
    public void Over80Words_Flagged_AtFirstLine()
    {
        var finding = Assert.Single(_rule.Evaluate(RulesFile.Parse($"# Rules\n\n{Words(40)}\n{Words(41)}\n")));

        Assert.Equal(3, finding.Line);
        Assert.Equal("Paragraph of 81 words; limit is 80.", finding.Message);
    }

    [Fact]
    public void Exactly80Words_NotFlagged() => Assert.Empty(_rule.Evaluate(RulesFile.Parse(Words(80))));

    [Fact]
    public void BlankLineSplitsParagraphs() => Assert.Empty(_rule.Evaluate(RulesFile.Parse($"{Words(50)}\n\n{Words(50)}\n")));

    [Theory]
    [InlineData("- ")]
    [InlineData("1. ")]
    [InlineData("> ")]
    public void ListItemsAndQuotes_NotParagraphs(string prefix) =>
        Assert.Empty(_rule.Evaluate(RulesFile.Parse($"{prefix}{Words(100)}\n")));

    [Fact]
    public void ListItemContinuationLines_NotParagraphs() =>
        Assert.Empty(_rule.Evaluate(RulesFile.Parse($"4. **Usage.** {Words(10)}\n   {Words(50)}\n   {Words(50)}\n")));

    [Fact]
    public void ParagraphAfterList_Counted() =>
        Assert.Single(_rule.Evaluate(RulesFile.Parse($"- item\n  {Words(50)}\n\n{Words(90)}\n")));

    [Fact]
    public void DescriptiveParagraph_NotFlagged_InstructionParagraph_Flagged()
    {
        var finding = Assert.Single(_rule.Evaluate(Fixtures.Load("v3/R013-instruction-paragraph.md")));

        Assert.Equal(7, finding.Line);
    }

    [Fact]
    public void InlineCodeDoesNotInflateCount() =>
        Assert.Empty(_rule.Evaluate(RulesFile.Parse($"{Words(78)} `a b c d e f g h`\n")));
}

public class UnverifiableCrossReferenceTests
{
    private readonly UnverifiableCrossReference _rule = new();

    [Theory]
    [InlineData("- Format dates the usual way.")]
    [InlineData("- As mentioned above, run tests first.")]
    [InlineData("- Use the pattern described earlier.")]
    [InlineData("4. Add a Storybook story (see below)")]
    [InlineData("- Name branches the way we always do.")]
    [InlineData("- Deploy like before.")]
    [InlineData("- Follow our usual process for releases.")]
    public void Flags(string line) => Assert.Single(_rule.Evaluate(RulesFile.Parse(line)));

    [Theory]
    [InlineData("- See below: [Testing](#testing).")]
    [InlineData("- As described above in `Build`, run make.")]
    [InlineData("- Use the pattern described in \"Error handling\" above.")]
    [InlineData("- See the **Testing** section below.")]
    [InlineData("- Put new tests below the existing ones.")]
    [InlineData("- The list above is exhaustive.")]
    public void DoesNotFlag(string line) => Assert.Empty(_rule.Evaluate(RulesFile.Parse(line)));
}
