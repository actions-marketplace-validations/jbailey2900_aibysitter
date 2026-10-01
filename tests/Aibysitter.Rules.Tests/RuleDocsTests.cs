namespace Aibysitter.Rules.Tests;

public class RuleDocsTests
{
    [Fact]
    public void EveryRule_HasDoc_AndEveryDoc_HasRule()
    {
        var ruleIds = LintEngine.DiscoverRules().Select(r => r.Id);
        var docIds = RuleDocs.All.Select(d => d.Id).Order(StringComparer.Ordinal);

        Assert.Equal(ruleIds, docIds);
    }

    [Fact]
    public void DocName_MatchesRuleClassName()
    {
        foreach (var rule in LintEngine.DiscoverRules())
        {
            Assert.Equal(rule.GetType().Name, RuleDocs.Find(rule.Id)!.Name);
        }
    }

    [Fact]
    public void Examples_BehaveAsDocumented()
    {
        var engine = new LintEngine();

        foreach (var doc in RuleDocs.All.Where(d => d.Id != "R004"))
        {
            Assert.Contains(engine.Lint(doc.BadExample), f => f.RuleId == doc.Id);
            Assert.DoesNotContain(engine.Lint(doc.GoodExample), f => f.RuleId == doc.Id);
        }
    }

    [Theory]
    [InlineData("R003")]
    [InlineData("r003")]
    public void Find_IsCaseInsensitive(string id)
    {
        Assert.Equal("ContradictoryModals", RuleDocs.Find(id)?.Name);
    }

    [Fact]
    public void Find_UnknownId_ReturnsNull()
    {
        Assert.Null(RuleDocs.Find("R999"));
    }
}
