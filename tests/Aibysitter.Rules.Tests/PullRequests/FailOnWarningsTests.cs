using Aibysitter.Rules.PullRequests;
using static Aibysitter.Rules.Tests.PullRequests.PrTestData;

namespace Aibysitter.Rules.Tests.PullRequests;

public class FailOnWarningsTests
{
    private sealed class OneFinding(string id, Severity severity, Severity? findingSeverity = null) : IPullRequestCheck
    {
        public string Id => id;
        public string Title => id;
        public Severity Severity => severity;

        public IEnumerable<PullRequestFinding> Evaluate(PullRequestContext context) =>
            [new PullRequestFinding(id, "src/A.cs", 1, "m", "f", findingSeverity)];
    }

    private static ReviewConclusion Conclude(string conclusion, IPullRequestCheck check) =>
        new PullRequestReviewer([check]).Review(Context(RepoConfig.Parse($$"""{ "conclusion": "{{conclusion}}" }""").Config, Added("src/A.cs", "x"))).Conclusion;

    [Theory]
    [InlineData("advisory", Severity.Error, ReviewConclusion.Neutral)]
    [InlineData("advisory", Severity.Warning, ReviewConclusion.Neutral)]
    [InlineData("fail-on-errors", Severity.Error, ReviewConclusion.Failure)]
    [InlineData("fail-on-errors", Severity.Warning, ReviewConclusion.Neutral)]
    [InlineData("fail-on-errors", Severity.Info, ReviewConclusion.Neutral)]
    [InlineData("fail-on-warnings", Severity.Error, ReviewConclusion.Failure)]
    [InlineData("fail-on-warnings", Severity.Warning, ReviewConclusion.Failure)]
    [InlineData("fail-on-warnings", Severity.Info, ReviewConclusion.Neutral)]
    public void Conclusion_BySeverityAndMode(string mode, Severity severity, ReviewConclusion expected) =>
        Assert.Equal(expected, Conclude(mode, new OneFinding("P015", severity)));

    [Theory]
    [InlineData("fail-on-warnings", Severity.Warning, ReviewConclusion.Neutral)]
    [InlineData("fail-on-warnings", Severity.Error, ReviewConclusion.Failure)]
    [InlineData("fail-on-errors", Severity.Error, ReviewConclusion.Failure)]
    public void P014_FailsOnlyAtRuleError(string mode, Severity ruleSeverity, ReviewConclusion expected) =>
        Assert.Equal(expected, Conclude(mode, new OneFinding(RulesFileLint.CheckId, Severity.Warning, ruleSeverity)));

    [Fact]
    public void NoFindings_Success() =>
        Assert.Equal(ReviewConclusion.Success, new PullRequestReviewer([]).Review(Context(RepoConfig.Parse("""{ "conclusion": "fail-on-warnings" }""").Config)).Conclusion);

    [Theory]
    [InlineData("advisory", ConclusionMode.Advisory, false)]
    [InlineData("fail-on-errors", ConclusionMode.FailOnErrors, true)]
    [InlineData("fail-on-warnings", ConclusionMode.FailOnWarnings, true)]
    public void Parse_ConclusionValues(string value, ConclusionMode mode, bool failsCheck)
    {
        var (config, errors) = RepoConfig.Parse($$"""{ "conclusion": "{{value}}" }""");

        Assert.Empty(errors);
        Assert.Equal(mode, config.Conclusion);
        Assert.Equal(failsCheck, config.FailsCheck);
        Assert.Equal(value, RepoConfig.ConclusionName(mode));
    }

    [Fact]
    public void Parse_UnknownConclusion_ListsThreeValues() =>
        Assert.Contains(RepoConfig.Parse("""{ "conclusion": "fail-on-info" }""").Errors, e => e.Message.EndsWith("\"conclusion\" must be \"advisory\", \"fail-on-warnings\" or \"fail-on-errors\"", StringComparison.Ordinal));
}
