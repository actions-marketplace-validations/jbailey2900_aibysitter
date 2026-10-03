using Aibysitter.Rules.Rules;

namespace Aibysitter.Rules.Tests;

public class VagueVerbsTests
{
    private readonly VagueVerbs _rule = new();

    [Fact]
    public void Fixture_ReportsExpectedLines()
    {
        var findings = _rule.Evaluate(Fixtures.Load("R002-vague-verbs.md")).ToList();

        Assert.Equal(3, findings.Count);
        Assert.Equal(new[] { 3, 5, 7 }, findings.Select(f => f.Line));
        Assert.All(findings, f => Assert.Equal("R002", f.RuleId));
    }

    [Fact]
    public void CleanFixture_ReportsNothing()
    {
        Assert.Empty(_rule.Evaluate(Fixtures.Load("clean.md")));
    }

    [Theory]
    [InlineData("- Handle errors properly.")]
    [InlineData("- Always handle edge cases.")]
    [InlineData("- **Errors**: Handle them.")]
    [InlineData("- Ensure good performance.")]
    [InlineData("- Use appropriate log levels.")]
    [InlineData("- Add or update tests as needed.")]
    [InlineData("1. Clean up after yourself.")]
    [InlineData("Run the suite. Then manage the cache.")]
    [InlineData("- Always handle errors gracefully with proper HTTP status codes.")]
    [InlineData("- Add to `marketplace.json` in the appropriate collection.")]
    public void Flags_InstructionPosition(string line)
    {
        Assert.Single(_rule.Evaluate(RulesFile.Parse(line)));
    }

    [Theory]
    [InlineData("Commands handle npm package publishing.")]
    [InlineData("- Handle errors gracefully with a dedicated error boundary.")]
    [InlineData("- Optimize to reduce network waterfalls.")]
    [InlineData("- Verify all functions were properly migrated.")]
    [InlineData("- Ensure proper cleanup to avoid zombie processes.")]
    [InlineData("- Include `scripts/` and `references/` as needed.")]
    [InlineData("- Clean up temp secret files.")]
    [InlineData("- `npm start` - ensure server starts successfully.")]
    [InlineData("- `manage_custom_operation` — Manage custom operations for stateful resources")]
    [InlineData("**Model Management** (`core/manage/`):")]
    [InlineData("| Handle | Owner |")]
    [InlineData("- Ensure tests pass (`npx gulp test`).")]
    [InlineData("- Extend appropriate error classes (ChainError, ValidationFailedError).")]
    [InlineData("- Ensure American English spelling, e.g. behavior.")]
    [InlineData("- Clean up files, external resources, and environment changes.")]
    [InlineData("- Use appropriate log levels:")]
    [InlineData("- Ensure the key is set.")]
    [InlineData("- Ensure no uncommitted changes.")]
    [InlineData("- Use React.memo when appropriate.")]
    [InlineData("- Keep dependencies workspace-appropriate.")]
    [InlineData("- Optimize for fast delivery.")]
    [InlineData("5. **Clean up** - Clear stashes, prune remote branches.")]
    [InlineData("- Add guidance to the appropriate AGENTS.md.")]
    [InlineData("Claude Code tool restrictions are not working properly.")]
    [InlineData("- Test script selects appropriate compose file.")]
    public void Skips_NonInstructionsAndConcreteTargets(string line)
    {
        Assert.Empty(_rule.Evaluate(RulesFile.Parse(line)));
    }
}
