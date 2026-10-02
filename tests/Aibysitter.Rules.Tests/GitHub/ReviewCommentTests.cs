using Aibysitter.Rules.PullRequests;
using Aibysitter.Web.GitHub;
using Microsoft.Extensions.Logging.Abstractions;
using static Aibysitter.Rules.Tests.PullRequests.PrTestData;

namespace Aibysitter.Rules.Tests.GitHub;

public class ReviewCommentTests
{
    private static readonly PullRequestRef Pr = new(42, "o", "r", 7, "abcdef0123");
    private static readonly ReviewJob Job = new(Pr, 99, "delivery-1");

    private static ReviewProcessor Processor(FakeGitHubGateway fake) =>
        new(fake, new PullRequestReviewer(), NullLogger<ReviewProcessor>.Instance);

    private static ReviewCommentPublisher Publisher(FakeGitHubGateway fake) => new(fake, NullLogger.Instance);

    private static CheckRunReport Report(int annotations, string summary = "| Check | Severity | Findings |") => new(
        ReviewConclusion.Neutral,
        $"{annotations} findings",
        summary,
        Enumerable.Range(1, annotations).Select(i => new CheckRunAnnotation($"src/My File{i}.cs", i, Severity.Error, "P001 Placeholder identifiers", "Line one\nline two", "")).ToList());

    [Theory]
    [InlineData("{\"comment\": true}", true, 0)]
    [InlineData("{\"comment\": false}", false, 0)]
    [InlineData("{}", false, 0)]
    [InlineData("{\"comment\": \"yes\"}", false, 1)]
    public void Config_CommentKey(string json, bool comment, int errors)
    {
        var (config, list) = RepoConfig.Parse(json);

        Assert.Equal(comment, config.Comment);
        Assert.Equal(errors, list.Count);
        if (errors > 0)
        {
            Assert.Equal(".github/aibysitter.json: \"comment\" must be true or false", list[0].Message);
        }
    }

    [Fact]
    public void Body_MarkerTitleCommitSummaryLinks()
    {
        var body = ReviewComment.Build(Report(2), Pr, 99);

        Assert.StartsWith("<!-- aibysitter-review -->\n**Aibysitter review: 2 findings** · commit `abcdef0`\n\n| Check | Severity | Findings |\n", body);
        Assert.Contains("- [`src/My File1.cs:1`](https://github.com/o/r/blob/abcdef0123/src/My%20File1.cs#L1) P001 Placeholder identifiers: Line one line two\n", body);
        Assert.EndsWith("\n[View the check run](https://github.com/o/r/runs/99)\n", body);
        Assert.DoesNotContain("more in the check run", body);
    }

    [Fact]
    public void Body_Lists25_ThenCountsTheRest()
    {
        var body = ReviewComment.Build(Report(30), Pr, 99);

        Assert.Equal(25, body.Split('\n').Count(l => l.StartsWith("- [`", StringComparison.Ordinal)));
        Assert.Contains("\nand 5 more in the check run.\n", body);
    }

    [Fact]
    public void Body_StaysUnderTheLimit_AndKeepsTheLink()
    {
        var body = ReviewComment.Build(Report(25, new string('x', 70_000)), Pr, 99);

        Assert.True(body.Length <= ReviewComment.MaxLength);
        Assert.EndsWith("[View the check run](https://github.com/o/r/runs/99)", body);
    }

    [Fact]
    public async Task Publish_CreatesWhenNone()
    {
        var fake = new FakeGitHubGateway();

        Assert.Null(await Publisher(fake).PublishAsync(Pr, "<!-- aibysitter-review -->\nA", hasFindings: true, CancellationToken.None));

        var comment = Assert.Single(fake.Comments);
        Assert.Equal(("aibysitter[bot]", "<!-- aibysitter-review -->\nA"), (comment.Author, comment.Body));
    }

    [Fact]
    public async Task Publish_UpdatesOurs_IgnoresTheMarkerFromAPerson()
    {
        var fake = new FakeGitHubGateway();
        fake.Comments.Add(new IssueCommentInfo(1, "someone", "<!-- aibysitter-review -->\nfake", DateTimeOffset.UnixEpoch));
        fake.Comments.Add(new IssueCommentInfo(2, "aibysitter[bot]", "<!-- aibysitter-review -->\nold", DateTimeOffset.UnixEpoch.AddDays(1)));

        await Publisher(fake).PublishAsync(Pr, "<!-- aibysitter-review -->\nnew", hasFindings: true, CancellationToken.None);

        Assert.Equal(["<!-- aibysitter-review -->\nfake", "<!-- aibysitter-review -->\nnew"], fake.Comments.Select(c => c.Body));
        Assert.DoesNotContain("create comment", fake.Calls);
    }

    [Fact]
    public async Task Publish_KeepsOldest_DeletesDuplicates()
    {
        var fake = new FakeGitHubGateway();
        fake.Comments.Add(new IssueCommentInfo(5, "aibysitter[bot]", "<!-- aibysitter-review -->\nb", DateTimeOffset.UnixEpoch.AddDays(2)));
        fake.Comments.Add(new IssueCommentInfo(4, "aibysitter[bot]", "<!-- aibysitter-review -->\na", DateTimeOffset.UnixEpoch.AddDays(1)));

        await Publisher(fake).PublishAsync(Pr, "<!-- aibysitter-review -->\nnew", hasFindings: true, CancellationToken.None);

        var comment = Assert.Single(fake.Comments);
        Assert.Equal((4L, "<!-- aibysitter-review -->\nnew"), (comment.Id, comment.Body));
    }

    [Fact]
    public async Task Publish_RaceOnCreate_LeavesOne()
    {
        var fake = new FakeGitHubGateway { RacingCommentBody = "<!-- aibysitter-review -->\nother process" };

        await Publisher(fake).PublishAsync(Pr, "<!-- aibysitter-review -->\nmine", hasFindings: true, CancellationToken.None);

        Assert.Equal("<!-- aibysitter-review -->\nmine", Assert.Single(fake.Comments).Body);
    }

    [Fact]
    public async Task Publish_NoFindings_NoNewComment_ButUpdatesExisting()
    {
        var empty = new FakeGitHubGateway();
        await Publisher(empty).PublishAsync(Pr, "<!-- aibysitter-review -->\nNo findings", hasFindings: false, CancellationToken.None);
        Assert.Empty(empty.Comments);

        var existing = new FakeGitHubGateway();
        existing.Comments.Add(new IssueCommentInfo(1, "aibysitter[bot]", "<!-- aibysitter-review -->\n3 findings", DateTimeOffset.UnixEpoch));
        await Publisher(existing).PublishAsync(Pr, "<!-- aibysitter-review -->\nNo findings", hasFindings: false, CancellationToken.None);
        Assert.Equal("<!-- aibysitter-review -->\nNo findings", Assert.Single(existing.Comments).Body);
    }

    [Fact]
    public async Task Publish_Forbidden_ReturnsPermissionNote()
    {
        var fake = new FakeGitHubGateway { ForbidComments = true };

        Assert.Equal(ReviewCommentPublisher.PermissionNote, await Publisher(fake).PublishAsync(Pr, "x", hasFindings: true, CancellationToken.None));
    }

    [Fact]
    public async Task Processor_CommentOn_PostsBeforeCompleting()
    {
        var fake = new FakeGitHubGateway();
        fake.Files.Add(Added("src/A.cs", "var k = ::KEY::;"));
        fake.Contents[RepoConfig.FilePath] = "{\"comment\": true}";

        await Processor(fake).ProcessAsync(Job, CancellationToken.None);
        await fake.Completed.Task;

        var calls = fake.Calls.ToList();
        Assert.True(calls.IndexOf("create comment") < calls.IndexOf("complete 99"));
        var body = Assert.Single(fake.Comments).Body;
        Assert.Contains("**Aibysitter review: 1 finding (1 error, 0 warnings)** · commit `abcdef0`", body);
        Assert.Contains("(https://github.com/o/r/blob/abcdef0123/src/A.cs#L1) P001", body);
    }

    [Fact]
    public async Task Processor_CommentOff_NoCommentCalls()
    {
        var fake = new FakeGitHubGateway();
        fake.Files.Add(Added("src/A.cs", "var k = ::KEY::;"));

        await Processor(fake).ProcessAsync(Job, CancellationToken.None);
        await fake.Completed.Task;

        Assert.DoesNotContain(fake.Calls, c => c.Contains("comment", StringComparison.Ordinal) || c == "slug");
    }

    [Fact]
    public async Task Processor_Forbidden_NoteInSummary_ConclusionUnchanged()
    {
        var fake = new FakeGitHubGateway { ForbidComments = true };
        fake.Files.Add(Added("src/A.cs", "var k = ::KEY::;"));
        fake.Contents[RepoConfig.FilePath] = "{\"comment\": true, \"conclusion\": \"fail-on-errors\"}";

        await Processor(fake).ProcessAsync(Job, CancellationToken.None);
        var report = await fake.Completed.Task;

        Assert.Equal(ReviewConclusion.Failure, report.Conclusion);
        Assert.EndsWith("\n\n" + ReviewCommentPublisher.PermissionNote, report.Summary);
    }
}
