using Aibysitter.Rules.PullRequests;
using Aibysitter.Web.GitHub;
using Octokit;
using static Aibysitter.Rules.Tests.PullRequests.PrTestData;
using Severity = Aibysitter.Rules.Severity;

namespace Aibysitter.Rules.Tests.GitHub;

public class CheckRunReportTests
{
    private static readonly PullRequestReviewer Reviewer = new();

    private static CheckRunReport Build(RepoConfig config, IReadOnlyList<string> configErrors, params ChangedFile[] files) =>
        CheckRunReport.Build(Reviewer.Review(new PullRequestContext(files, config)), Reviewer.Checks, files, config, configErrors);

    [Fact]
    public void Clean_Success_NoAnnotations()
    {
        var report = Build(RepoConfig.Default, [], Added("src/A.cs", "public class A { }"));

        Assert.Equal(ReviewConclusion.Success, report.Conclusion);
        Assert.Equal("No findings", report.Title);
        Assert.Empty(report.Annotations);
        Assert.Contains("| P001 Placeholder identifiers | Error | 0 |", report.Summary);
        Assert.Contains("Conclusion mode: `advisory`. Scope: not declared.", report.Summary);
    }

    [Fact]
    public void Findings_BecomeAnnotations_WithTitleSeverityAndHint()
    {
        var report = Build(RepoConfig.Default, [], Added("src/A.cs", "var k = ::KEY::;", "// TODO later"));

        Assert.Equal("2 findings (1 error, 1 warning)", report.Title);
        Assert.Equal(
            new[] { ("src/A.cs", 1, Severity.Error, "P001 Placeholder identifiers"), ("src/A.cs", 2, Severity.Warning, "P002 TODO stubs") },
            report.Annotations.Select(a => (a.Path, a.Line, a.Severity, a.Title)));
        Assert.Equal("Replace with the real identifier or value.", report.Annotations[0].RawDetails);
    }

    [Fact]
    public void RemovedFileFindings_GoToSummary_NotAnnotations()
    {
        var config = RepoConfig.Parse("{\"scope\": [\"src/**\"]}").Config;
        var removed = new ChangedFile("docs/old.md", FileChangeStatus.Removed);

        var report = Build(config, [], removed);

        Assert.Empty(report.Annotations);
        Assert.Contains("Findings on removed files:", report.Summary);
        Assert.Contains("- `docs/old.md`: P004", report.Summary);
        Assert.Contains("Scope: `src/**`.", report.Summary);
    }

    [Fact]
    public void ConfigErrors_AreListedInSummary()
    {
        var (config, errors) = RepoConfig.Parse("{\"conclusion\": \"strict\"}");

        var report = Build(config, errors, Added("src/A.cs", "x"));

        Assert.Contains("Config errors (defaults used for these):", report.Summary);
        Assert.Contains("\"conclusion\" must be", report.Summary);
    }

    [Fact]
    public void AnnotationBatches_SplitAt50()
    {
        var lines = Enumerable.Range(1, 120).Select(i => $"var k{i} = ::KEY::;").ToArray();

        var report = Build(RepoConfig.Default, [], Added("src/A.cs", lines));

        Assert.Equal(new[] { 50, 50, 20 }, report.AnnotationBatches().Select(b => b.Count));
    }

    [Fact]
    public void ForError_IsNeutral_WithRetryHint()
    {
        var report = CheckRunReport.ForError(new TimeoutException("x"));

        Assert.Equal(ReviewConclusion.Neutral, report.Conclusion);
        Assert.Equal("Review failed", report.Title);
        Assert.Contains("TimeoutException", report.Summary);
        Assert.Contains("redeliver the webhook", report.Summary);
    }

    [Theory]
    [InlineData(Severity.Error, CheckAnnotationLevel.Failure)]
    [InlineData(Severity.Warning, CheckAnnotationLevel.Warning)]
    [InlineData(Severity.Info, CheckAnnotationLevel.Notice)]
    public void MapLevel(Severity severity, CheckAnnotationLevel expected)
    {
        Assert.Equal(expected, OctokitGitHubGateway.MapLevel(severity));
    }

    [Theory]
    [InlineData(ReviewConclusion.Success, CheckConclusion.Success)]
    [InlineData(ReviewConclusion.Neutral, CheckConclusion.Neutral)]
    [InlineData(ReviewConclusion.Failure, CheckConclusion.Failure)]
    public void MapConclusion(ReviewConclusion conclusion, CheckConclusion expected)
    {
        Assert.Equal(expected, OctokitGitHubGateway.MapConclusion(conclusion));
    }

    [Theory]
    [InlineData("added", FileChangeStatus.Added)]
    [InlineData("removed", FileChangeStatus.Removed)]
    [InlineData("renamed", FileChangeStatus.Renamed)]
    [InlineData("modified", FileChangeStatus.Modified)]
    [InlineData(null, FileChangeStatus.Modified)]
    public void MapStatus(string? status, FileChangeStatus expected)
    {
        Assert.Equal(expected, OctokitGitHubGateway.MapStatus(status));
    }

    [Fact]
    public void DisabledCheck_ShownAsDisabled()
    {
        var (config, errors) = RepoConfig.Parse("""{ "disable": ["P002"] }""");

        var report = Build(config, errors, Added("src/A.cs", "// TODO later"));

        Assert.Contains("| P002 TODO stubs | Warning | disabled |", report.Summary);
        Assert.Empty(report.Annotations);
    }
}
