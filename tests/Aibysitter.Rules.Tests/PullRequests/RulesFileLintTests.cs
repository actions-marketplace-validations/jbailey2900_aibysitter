using Aibysitter.Rules.PullRequests;
using static Aibysitter.Rules.Tests.PullRequests.PrTestData;

namespace Aibysitter.Rules.Tests.PullRequests;

public class RulesFileLintTests
{
    private readonly RulesFileLint _check = new();
    private readonly PullRequestReviewer _reviewer = new();

    private static ChangedFile Modified(string path, string head, params int[] addedLines) =>
        new(path, FileChangeStatus.Modified, BuildPatch(head.Split('\n'), addedLines), HeadContent: head);

    /// <summary>Patch with each added line at its head line number, context elsewhere.</summary>
    private static string BuildPatch(string[] lines, int[] added) =>
        $"@@ -1,{lines.Length - added.Length} +1,{lines.Length} @@\n" + string.Join("\n", lines.Select((l, i) => (added.Contains(i + 1) ? "+" : " ") + l));

    [Fact]
    public void AddedFile_AllFindings_WithRuleSeverity()
    {
        var file = Added("CLAUDE.md", "# Rules", "- Handle errors properly.", "- Try to keep it short.", "- Use Password=Hunter2Prod; for the db.");

        var findings = _check.Evaluate(Context(file)).ToList();

        Assert.Equal(new[] { "R002", "R007", "R009" }, findings.Select(f => f.Message[..4]));
        Assert.Equal(new Severity?[] { Severity.Warning, Severity.Warning, Severity.Error }, findings.Select(f => f.Severity));
        Assert.All(findings, f => Assert.Equal("P014", f.CheckId));
        Assert.StartsWith("R002 Vague verbs: Vague wording:", findings[0].Message);
    }

    [Fact]
    public void ModifiedFile_OnlyAddedLines_PlusWholeFileRules()
    {
        var head = "# Rules\n- Handle errors properly.\n- Try to keep it short.\n```\nunclosed";
        var file = Modified("AGENTS.md", head, 3);

        var ids = _check.Evaluate(Context(file)).Select(f => f.Message[..4]).ToList();

        Assert.Equal(new[] { "R007", "R012" }, ids);
    }

    [Fact]
    public void RemovedAndUnknownFiles_Skipped_MdcOnlyUnderCursorRules()
    {
        var findings = _check.Evaluate(Context(
            new ChangedFile("CLAUDE.md", FileChangeStatus.Removed, HeadContent: "- Handle errors properly."),
            Added("README.md", "- Handle errors properly."),
            Added("docs/card.mdc", "- Handle errors properly."),
            Added(".cursor/rules/api.mdc", "---", "description: API", "globs: src/**", "alwaysApply: false", "---", "- Handle errors properly."))).ToList();

        Assert.Equal(".cursor/rules/api.mdc", Assert.Single(findings).Path);
    }

    [Fact]
    public void FormatFromPath_RunsR015ForMdc()
    {
        var findings = _check.Evaluate(Context(Added(".cursor/rules/x.mdc", "# Rules", "- Use tabs."))).ToList();

        Assert.StartsWith("R015 Frontmatter fields: Cursor rule has no frontmatter.", Assert.Single(findings).Message);
    }

    [Fact]
    public void NoHeadContent_Skipped() =>
        Assert.Empty(_check.Evaluate(Context(new ChangedFile("CLAUDE.md", FileChangeStatus.Added, "@@ -0,0 +1,1 @@\n+- Handle errors properly."))));

    [Fact]
    public void Precedence_InFileSuppression_ThenConfigRuleDisable_ThenCheckDisable()
    {
        var file = Added("CLAUDE.md", "<!-- aibysitter-disable R002 -->", "- Handle errors properly.", "- Try to keep it short.", "- You are a senior engineer.");

        Assert.Equal(new[] { "R007", "R010" }, _check.Evaluate(Context(file)).Select(f => f.Message[..4]));

        var (ruleOff, errors) = RepoConfig.Parse("""{ "disable": ["R007"] }""");
        Assert.Empty(errors);
        Assert.Equal(new[] { "R010" }, _check.Evaluate(Context(ruleOff, file)).Select(f => f.Message[..4]));

        var (checkOff, _) = RepoConfig.Parse("""{ "disable": ["P014"] }""");
        Assert.DoesNotContain(_reviewer.Review(Context(checkOff, file)).Findings, f => f.CheckId == "P014");
    }

    [Fact]
    public void RuleDisable_DoesNotDisableChecks()
    {
        var (config, _) = RepoConfig.Parse("""{ "disable": ["R002"] }""");

        Assert.True(config.IsEnabled("P002"));
        Assert.False(config.IsEnabled("R002"));
    }

    [Fact]
    public void FailOnErrors_ErrorRuleFails_WarningRuleDoesNot()
    {
        var (config, _) = RepoConfig.Parse("""{ "conclusion": "fail-on-errors" }""");

        var warningOnly = _reviewer.Review(Context(config, Added("CLAUDE.md", "- Handle errors properly.")));
        var withError = _reviewer.Review(Context(config, Added("CLAUDE.md", "- Run:", "```", "dotnet test")));

        Assert.Equal(ReviewConclusion.Neutral, warningOnly.Conclusion);
        Assert.Equal(ReviewConclusion.Failure, withError.Conclusion);
    }
}
