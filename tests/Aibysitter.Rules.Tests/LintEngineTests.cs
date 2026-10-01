namespace Aibysitter.Rules.Tests;

public class LintEngineTests
{
    [Fact]
    public void DiscoverRules_FindsEveryRule()
    {
        var ids = LintEngine.DiscoverRules().Select(r => r.Id);

        Assert.Equal(new[] { "R001", "R002", "R003", "R004", "R005", "R007", "R008", "R009", "R012" }, ids);
    }

    [Fact]
    public void Lint_ReturnsFindingsSortedByLine()
    {
        var findings = new LintEngine().Lint(Fixtures.Load("R003-contradictory-modals.md"));
        var lines = findings.Select(f => f.Line).ToList();

        Assert.NotEmpty(findings);
        Assert.Equal(lines.Order(), lines);
    }

    [Fact]
    public void Lint_CleanFixture_ReportsNothing()
    {
        Assert.Empty(new LintEngine().Lint(Fixtures.Load("clean.md")));
    }
}
