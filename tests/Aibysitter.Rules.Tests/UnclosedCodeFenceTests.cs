using Aibysitter.Rules.Rules;

namespace Aibysitter.Rules.Tests;

public class UnclosedCodeFenceTests
{
    private readonly UnclosedCodeFence _rule = new();

    [Fact]
    public void Unclosed_ReportedAtOpeningLine()
    {
        var finding = Assert.Single(_rule.Evaluate(RulesFile.Parse("# Rules\nRun:\n```bash\ndotnet test\n- Use tabs.\n")));

        Assert.Equal(3, finding.Line);
        Assert.Equal("Code fence opened on line 3 is never closed; the remaining 2 lines render as code.", finding.Message);
    }

    [Theory]
    [InlineData("```\ncode\n```\n")]
    [InlineData("~~~\ncode\n~~~\n")]
    [InlineData("````\n```\nnested\n```\n````\n")]
    [InlineData("- Use tabs.\n")]
    public void Closed_NoFinding(string text)
    {
        Assert.Empty(_rule.Evaluate(RulesFile.Parse(text)));
    }

    [Fact]
    public void TildeFence_NotClosedByBackticks()
    {
        Assert.Single(_rule.Evaluate(RulesFile.Parse("~~~\ncode\n```\n")));
    }

    [Fact]
    public void LongerFence_NotClosedByShorterRun()
    {
        var file = RulesFile.Parse("````\n```\n- nested\n```\n````\n- Use tabs.\n");

        Assert.All(file.Lines.Take(5), l => Assert.True(l.IsInCodeFence));
        Assert.False(file.Lines[5].IsInCodeFence);
        Assert.Null(file.UnclosedFenceLine);
    }

    [Fact]
    public void FenceWithInfoString_DoesNotClose()
    {
        Assert.Single(_rule.Evaluate(RulesFile.Parse("```\ncode\n```bash\n")));
    }
}
