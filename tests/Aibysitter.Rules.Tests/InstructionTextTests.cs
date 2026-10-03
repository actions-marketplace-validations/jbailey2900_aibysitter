namespace Aibysitter.Rules.Tests;

public class InstructionTextTests
{
    [Theory]
    [InlineData("- Run tests before commit.")]
    [InlineData("Never commit secrets.")]
    [InlineData("**Testing:** use xUnit.")]
    [InlineData("If the build fails, stop and report it.")]
    [InlineData("State lives in `.claude/state.md` — always clean up on exit.")]
    [InlineData("The agent must not push to main.")]
    [InlineData("Node 22 is required.")]
    public void Instructions(string text) => Assert.True(InstructionText.IsInstruction(text));

    [Theory]
    [InlineData("This repository holds the gateway service.")]
    [InlineData("**Token refresh**: Tokens auto-refresh when expired.")]
    [InlineData("In this tutorial, you will extend the app.")]
    [InlineData("The last expression in a block is always returned.")]
    [InlineData("- Dependencies are displayed with status indicators.")]
    public void NotInstructions(string text) => Assert.False(InstructionText.IsInstruction(text));

    [Fact]
    public void Sentences_DoNotSplitAfterAbbreviations()
    {
        Assert.Equal(["Use a runner, e.g. xUnit, for tests.", "Keep it fast."], InstructionText.Sentences("Use a runner, e.g. xUnit, for tests. Keep it fast."));
    }

    [Fact]
    public void Units_ListItemsAndParagraphs()
    {
        var file = RulesFile.Parse("# T\n- one\n  cont\n- two\n\npara a\npara b\n| x |\nafter\n");

        Assert.Equal([[2, 3], [4], [6, 7], [9]], InstructionText.Units(file).Select(u => u.Select(l => l.Number).ToArray()).ToArray());
    }
}
