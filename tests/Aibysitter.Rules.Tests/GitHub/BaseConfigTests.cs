using Aibysitter.Rules.PullRequests;
using Aibysitter.Web.GitHub;
using Microsoft.Extensions.Logging.Abstractions;
using static Aibysitter.Rules.Tests.PullRequests.PrTestData;

namespace Aibysitter.Rules.Tests.GitHub;

/// <summary>Config is read from the base commit; a pull request cannot change how it is itself reviewed.</summary>
public class BaseConfigTests
{
    private static readonly ReviewJob Job = new(new PullRequestRef(42, "o", "r", 7, "abcdef0123", "base0123456"), 99, "delivery-1");

    private static async Task<CheckRunReport> Review(FakeGitHubGateway fake, ReviewJob? job = null)
    {
        await new ReviewProcessor(fake, new PullRequestReviewer(), NullLogger<ReviewProcessor>.Instance).ProcessAsync(job ?? Job, CancellationToken.None);
        return await fake.Completed.Task;
    }

    private static ChangedFile ConfigEdit(string headJson) =>
        new(RepoConfig.FilePath, FileChangeStatus.Modified, "@@ -1,1 +1,1 @@\n-{}\n+" + headJson.ReplaceLineEndings(" "));

    [Fact]
    public async Task SandboxCase_PrRelaxesConfig_ReviewedWithBase_P004FlagsConfigEdit_Fails()
    {
        var fake = new FakeGitHubGateway();
        fake.BaseContents[RepoConfig.FilePath] = """{ "scope": ["src/**", "tests/**"], "conclusion": "fail-on-errors" }""";
        fake.Contents[RepoConfig.FilePath] = """{ "conclusion": "advisory" }""";
        fake.Files.Add(Added("src/OrderService.cs", "var x = 1;"));
        fake.Files.Add(ConfigEdit("""{ "conclusion": "advisory" }"""));

        var report = await Review(fake);

        Assert.Equal(ReviewConclusion.Failure, report.Conclusion);
        Assert.Contains(report.Annotations, a => a.Path == RepoConfig.FilePath && a.Title.StartsWith("P004", StringComparison.Ordinal));
        Assert.StartsWith(ReviewProcessor.ConfigChangedNote, report.Summary, StringComparison.Ordinal);
        Assert.Contains("Conclusion mode: `fail-on-errors`", report.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrDisablesCheck_StillRuns()
    {
        var fake = new FakeGitHubGateway();
        fake.Contents[RepoConfig.FilePath] = """{ "disable": ["P001"] }""";
        fake.Files.Add(Added("src/Billing.cs", "var key = \"YOUR_API_KEY\";"));
        fake.Files.Add(ConfigEdit("""{ "disable": ["P001"] }"""));

        var report = await Review(fake);

        Assert.Contains(report.Annotations, a => a.Title.StartsWith("P001", StringComparison.Ordinal));
        Assert.StartsWith(ReviewProcessor.ConfigChangedNote, report.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrAddsIgnore_DoesNotApply()
    {
        var fake = new FakeGitHubGateway();
        fake.Contents[RepoConfig.FilePath] = """{ "ignore": ["src/**"] }""";
        fake.Files.Add(Added("src/Billing.cs", "var key = \"YOUR_API_KEY\";"));
        fake.Files.Add(ConfigEdit("""{ "ignore": ["src/**"] }"""));

        var report = await Review(fake);

        Assert.Contains(report.Annotations, a => a.Path == "src/Billing.cs");
        Assert.DoesNotContain("Ignored by config", report.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangedConfig_HeadErrorsAnnotated()
    {
        var fake = new FakeGitHubGateway();
        fake.Contents[RepoConfig.FilePath] = """{ "conclusion": "sometimes" }""";
        fake.Files.Add(ConfigEdit("""{ "conclusion": "sometimes" }"""));

        var report = await Review(fake);

        Assert.Contains(report.Annotations, a => a.IsConfigError && a.Message.Contains("\"conclusion\" must be", StringComparison.Ordinal));
        Assert.Contains("base content .github/aibysitter.json", fake.Calls);
        Assert.Contains("content .github/aibysitter.json", fake.Calls);
    }

    [Fact]
    public async Task UnchangedConfig_BaseOnly_NoNote()
    {
        var fake = new FakeGitHubGateway();
        fake.BaseContents[RepoConfig.FilePath] = """{ "ignore": [{ "paths": ["tests/**"], "checks": ["P005"] }] }""";
        fake.Files.Add(Added("tests/FixtureTests.cs", "var cs = \"Server=db;Password=Hunter2Prod;\";"));
        fake.Files.Add(Added("src/Real.cs", "var y = 2;"));

        var report = await Review(fake);

        Assert.DoesNotContain(fake.Calls, c => c == "content .github/aibysitter.json");
        Assert.DoesNotContain(report.Annotations, a => a.Title.StartsWith("P005", StringComparison.Ordinal));
        Assert.DoesNotContain(ReviewProcessor.ConfigChangedNote, report.Summary, StringComparison.Ordinal);
        Assert.Contains("Ignored by config: 1 file (`tests/**` for P005).", report.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task JobWithoutBaseSha_ReadsHead()
    {
        var fake = new FakeGitHubGateway();
        fake.Contents[RepoConfig.FilePath] = """{ "conclusion": "fail-on-errors" }""";
        fake.Files.Add(Added("src/Billing.cs", "var key = \"YOUR_API_KEY\";"));

        var report = await Review(fake, Job with { PullRequest = Job.PullRequest with { BaseSha = null } });

        Assert.Equal(ReviewConclusion.Failure, report.Conclusion);
        Assert.DoesNotContain(fake.Calls, c => c.StartsWith("base content", StringComparison.Ordinal));
    }
}
