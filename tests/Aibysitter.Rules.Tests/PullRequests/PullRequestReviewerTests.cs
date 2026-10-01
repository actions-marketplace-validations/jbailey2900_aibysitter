using Aibysitter.Rules.PullRequests;
using static Aibysitter.Rules.Tests.PullRequests.PrTestData;

namespace Aibysitter.Rules.Tests.PullRequests;

public class PullRequestReviewerTests
{
    private readonly PullRequestReviewer reviewer = new();

    [Fact]
    public void DiscoverChecks_FindsEveryCheck()
    {
        Assert.Equal(new[] { "P001", "P002", "P003", "P004", "P005", "P006", "P007", "P008", "P009", "P010", "P011", "P012", "P013" }, PullRequestReviewer.DiscoverChecks().Select(c => c.Id));
    }

    [Fact]
    public void CleanPullRequest_Succeeds()
    {
        var review = reviewer.Review(Context(Added("src/A.cs", "public class A { }")));

        Assert.Empty(review.Findings);
        Assert.Equal(ReviewConclusion.Success, review.Conclusion);
    }

    [Fact]
    public void Findings_SortedByPathThenLineThenCheck()
    {
        var review = reviewer.Review(Context(
            Added("src/B.cs", "// TODO later", "var k = ::KEY::; // TODO"),
            Added("src/A.cs", "throw new NotImplementedException();")));

        Assert.Equal(
            new[] { ("src/A.cs", 1, "P002"), ("src/B.cs", 1, "P002"), ("src/B.cs", 2, "P001"), ("src/B.cs", 2, "P002") },
            review.Findings.Select(f => (f.Path, f.Line, f.CheckId)));
    }

    [Fact]
    public void Advisory_WithErrorFindings_IsNeutral()
    {
        var review = reviewer.Review(Context(Added("src/A.cs", "var k = ::KEY::;")));

        Assert.Equal(ReviewConclusion.Neutral, review.Conclusion);
    }

    [Fact]
    public void FailOnErrors_WithErrorFinding_Fails()
    {
        var config = RepoConfig.Parse("{\"conclusion\": \"fail-on-errors\"}").Config;

        var review = reviewer.Review(Context(config, Added("src/A.cs", "var k = ::KEY::;")));

        Assert.Equal(ReviewConclusion.Failure, review.Conclusion);
    }

    [Fact]
    public void FailOnErrors_WithOnlyWarnings_IsNeutral()
    {
        var config = RepoConfig.Parse("{\"conclusion\": \"fail-on-errors\"}").Config;

        var review = reviewer.Review(Context(config, Added("src/A.cs", "// TODO later")));

        Assert.Equal("P002", Assert.Single(review.Findings).CheckId);
        Assert.Equal(ReviewConclusion.Neutral, review.Conclusion);
    }

    [Fact]
    public void DisabledCheck_DoesNotRun()
    {
        var (config, _) = RepoConfig.Parse("""{ "disable": ["P002"], "conclusion": "fail-on-errors" }""");

        var review = reviewer.Review(Context(config, Added("src/A.cs", "// TODO later", "var k = ::KEY::;")));

        Assert.Equal("P001", Assert.Single(review.Findings).CheckId);
        Assert.Equal(ReviewConclusion.Failure, review.Conclusion);
    }

    [Fact]
    public void AllFindingsDisabled_Succeeds()
    {
        var (config, _) = RepoConfig.Parse("""{ "disable": ["P002"] }""");

        Assert.Equal(ReviewConclusion.Success, reviewer.Review(Context(config, Added("src/A.cs", "// TODO later"))).Conclusion);
    }
}
