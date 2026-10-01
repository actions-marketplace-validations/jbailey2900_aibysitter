namespace Aibysitter.Rules.Tests;

public class SuppressionTests
{
    private static readonly LintEngine Engine = new();

    [Fact]
    public void FileWide_SuppressesRuleOnEveryLine()
    {
        var result = Engine.Analyze("<!-- aibysitter-disable R002 -->\n- Handle errors.\n- Manage state.\n");

        Assert.Empty(result.Findings);
        Assert.Equal(new[] { 2, 3 }, result.Suppressed.Select(f => f.Line));
    }

    [Fact]
    public void FileWide_AppliesFromAnyPosition()
    {
        var result = Engine.Analyze("- Handle errors.\n<!-- aibysitter-disable R002 -->\n");

        Assert.Empty(result.Findings);
        Assert.Single(result.Suppressed);
    }

    [Fact]
    public void NextLine_SuppressesOnlyTheFollowingLine()
    {
        var result = Engine.Analyze("<!-- aibysitter-disable-next-line R002 -->\n- Handle errors.\n- Manage state.\n");

        Assert.Equal(3, Assert.Single(result.Findings).Line);
        Assert.Equal(2, Assert.Single(result.Suppressed).Line);
    }

    [Fact]
    public void ListedIds_OnlyThoseRulesSuppressed()
    {
        var result = Engine.Analyze("<!-- aibysitter-disable-next-line R001, R005 -->\n- Handle errors because they happen.\n");

        Assert.Equal("R002", Assert.Single(result.Findings).RuleId);
        Assert.Equal("R001", Assert.Single(result.Suppressed).RuleId);
    }

    [Theory]
    [InlineData("<!--aibysitter-disable r002-->")]
    [InlineData("  <!-- AIBYSITTER-DISABLE R002 -->  ")]
    public void Directive_IsCaseAndSpacingTolerant(string directive)
    {
        Assert.Empty(Engine.Analyze($"{directive}\n- Handle errors.\n").Findings);
    }

    [Theory]
    [InlineData("<!-- aibysitter-disable -->")]
    [InlineData("<!-- aibysitter-disable all -->")]
    [InlineData("<!-- aibysitter-disable R2 -->")]
    [InlineData("Text <!-- aibysitter-disable R002 -->")]
    public void Malformed_DoesNotSuppress(string directive)
    {
        var result = Engine.Analyze($"{directive}\n- Handle errors.\n");

        Assert.Contains(result.Findings, f => f.RuleId == "R002");
        Assert.Empty(result.Suppressed);
    }

    [Fact]
    public void DirectiveInsideCodeFence_IsIgnored()
    {
        var result = Engine.Analyze("```\n<!-- aibysitter-disable R002 -->\n```\n- Handle errors.\n");

        Assert.Single(result.Findings);
        Assert.Empty(result.Suppressed);
    }

    [Fact]
    public void DirectiveLines_AreNotProse()
    {
        var file = RulesFile.Parse("<!-- aibysitter-disable-next-line R002 -->\n<!-- aibysitter-disable-next-line R002 -->\n- Use tabs.\n");

        Assert.All(file.Lines.Take(2), l => Assert.False(l.IsProse));
        Assert.Empty(Engine.Analyze(file).Findings);
    }

    [Fact]
    public void SuppressedFindings_DoNotCountTowardScore()
    {
        var result = Engine.Analyze("<!-- aibysitter-disable R002 -->\n- Handle errors.\n");

        Assert.Equal(100, Engine.Score(result.Findings).Value);
    }

    [Fact]
    public void Lint_ReturnsUnsuppressedFindingsOnly()
    {
        Assert.Empty(Engine.Lint("<!-- aibysitter-disable R002 -->\n- Handle errors.\n"));
    }
}
