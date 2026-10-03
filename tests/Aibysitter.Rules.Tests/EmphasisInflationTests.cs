using Aibysitter.Rules.Rules;

namespace Aibysitter.Rules.Tests;

public class EmphasisInflationTests
{
    private readonly EmphasisInflation _rule = new();

    private static string File(int emphasis, int plain) =>
        string.Join("\n", Enumerable.Range(1, emphasis).Select(n => $"- ALWAYS do thing {n}.")
            .Concat(Enumerable.Range(1, plain).Select(n => $"- Do thing {n}.")));

    [Theory]
    [InlineData(10, 3)]
    [InlineData(60, 3)]
    [InlineData(100, 5)]
    [InlineData(101, 6)]
    [InlineData(200, 10)]
    [InlineData(334, 17)]
    public void Allowed_Is5Per100Lines_Minimum3(int lines, int allowed)
    {
        Assert.Equal(allowed, EmphasisInflation.Allowed(lines));
    }

    [Fact]
    public void AtLimit_NoFinding()
    {
        Assert.Empty(_rule.Evaluate(RulesFile.Parse(File(3, 20))));
    }

    [Fact]
    public void OverLimit_OneFinding_AtFirstLineOverLimit()
    {
        var finding = Assert.Single(_rule.Evaluate(RulesFile.Parse(File(5, 20))));

        Assert.Equal(4, finding.Line);
        Assert.Equal("5 emphasis lines in 25 lines; limit is 3.", finding.Message);
    }

    [Theory]
    [InlineData("- IMPORTANT: commit often.")]
    [InlineData("- This is CRITICAL.")]
    [InlineData("- You MUST format.")]
    [InlineData("- NEVER push.")]
    [InlineData("- DO NOT push.")]
    [InlineData("- Tests are MANDATORY.")]
    [InlineData("- A review is REQUIRED.")]
    [InlineData("- Push to main!!")]
    [InlineData("## ALWAYS")]
    public void EmphasisLine_Detected(string line)
    {
        Assert.True(EmphasisInflation.IsEmphasisLine(line));
    }

    [Theory]
    [InlineData("- Always run tests.")]
    [InlineData("- This is important.")]
    [InlineData("- Set `MUST_RUN` to true.")]
    [InlineData("- Mustard is a condiment.")]
    public void EmphasisLine_NotDetected(string line)
    {
        Assert.False(EmphasisInflation.IsEmphasisLine(line));
    }

    [Fact]
    public void FileCitingRfc2119_KeywordsNotCounted_OtherEmphasisCounted()
    {
        var musts = string.Join("\n", Enumerable.Range(1, 20).Select(n => $"- Handlers MUST validate input {n}."));
        const string cite = "The key words MUST and REQUIRED are to be interpreted as described in RFC 2119.\n";

        Assert.Empty(_rule.Evaluate(RulesFile.Parse(cite + musts)));
        Assert.Single(_rule.Evaluate(RulesFile.Parse(cite + musts + "\n" + File(4, 0))));
    }

    [Fact]
    public void CodeBlockLines_NotCounted()
    {
        var text = "```\nALWAYS\nNEVER\nMUST\nIMPORTANT\n```\n- Use tabs.";

        Assert.Empty(_rule.Evaluate(RulesFile.Parse(text)));
    }
}
