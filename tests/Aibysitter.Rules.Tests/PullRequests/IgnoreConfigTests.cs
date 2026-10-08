using Aibysitter.Rules.PullRequests;

namespace Aibysitter.Rules.Tests.PullRequests;

public class IgnoreConfigTests
{
    private const string Example = """
        {
          "ignore": [
            "src/Aibysitter.Rules/PullRequests/PullRequestCheckDocs.cs",
            { "paths": ["tests/**"], "checks": ["P005", "p002"] }
          ]
        }
        """;

    [Fact]
    public void MixedEntries_Parse()
    {
        var (config, errors) = RepoConfig.Parse(Example);

        Assert.Empty(errors);
        Assert.Equal(2, config.Ignore.Count);
        Assert.Null(config.Ignore[0].Checks);
        Assert.Equal(["P002", "P005"], config.Ignore[1].Checks!.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("P001", "src/Aibysitter.Rules/PullRequests/PullRequestCheckDocs.cs", true)]
    [InlineData("P009", "src/Aibysitter.Rules/PullRequests/PullRequestCheckDocs.cs", true)]
    [InlineData("P005", "tests/Fixtures/Secrets.cs", true)]
    [InlineData("P002", "tests/Fixtures/Secrets.cs", true)]
    [InlineData("P001", "tests/Fixtures/Secrets.cs", false)]
    [InlineData("P005", "src/Db.cs", false)]
    [InlineData("P004", "src/Aibysitter.Rules/PullRequests/PullRequestCheckDocs.cs", false)]
    [InlineData("P013", "src/Aibysitter.Rules/PullRequests/PullRequestCheckDocs.cs", false)]
    public void IsIgnored_ByCheckAndPath(string checkId, string path, bool expected) =>
        Assert.Equal(expected, RepoConfig.Parse(Example).Config.IsIgnored(checkId, path));

    [Theory]
    [InlineData("""{ "ignore": "docs/**" }""", "must be an array")]
    [InlineData("""{ "ignore": [42] }""", "must be a path glob or an object")]
    [InlineData("""{ "ignore": [{ "checks": ["P005"] }] }""", "\"paths\" is required")]
    [InlineData("""{ "ignore": [{ "paths": [] }] }""", "must be a non-empty array")]
    [InlineData("""{ "ignore": [{ "paths": ["docs/**"], "checks": ["P999"] }] }""", "is not a known check ID")]
    [InlineData("""{ "ignore": [{ "paths": ["docs/**"], "checks": ["R002"] }] }""", "is not a known check ID")]
    [InlineData("""{ "ignore": [{ "paths": ["docs/**"], "checks": ["P004"] }] }""", "P004 checks paths, not content, and cannot be ignored")]
    [InlineData("""{ "ignore": [{ "paths": ["docs/**"], "except": ["P005"] }] }""", "\"except\" must be \"paths\" or \"checks\"")]
    public void InvalidEntries_ReportedWithLine_RestApplies(string json, string message)
    {
        var (config, errors) = RepoConfig.Parse(json);

        Assert.Contains(errors, e => e.Message.Contains(message, StringComparison.Ordinal));
        Assert.Empty(config.Ignore);
    }

    [Fact]
    public void InvalidEntry_DoesNotDropValidOnes()
    {
        var (config, errors) = RepoConfig.Parse("""{ "ignore": ["docs/**", { "paths": ["x"], "checks": ["P013"] }, "fixtures/**"] }""");

        Assert.Single(errors);
        Assert.Contains("ignore entry", errors[0].Message, StringComparison.Ordinal);
        Assert.Equal(2, config.Ignore.Count);
    }

    [Fact]
    public void MoreThanMaxEntries_RestSkipped()
    {
        var json = "{ \"ignore\": [" + string.Join(",", Enumerable.Range(0, RepoConfig.MaxIgnoreEntries + 5).Select(i => $"\"d{i}/**\"")) + "] }";

        var (config, errors) = RepoConfig.Parse(json);

        Assert.Equal(RepoConfig.MaxIgnoreEntries, config.Ignore.Count);
        Assert.Contains(errors, e => e.Message.Contains("more than 50 entries", StringComparison.Ordinal));
    }

    [Fact]
    public void Reviewer_SkipsIgnoredFilesForContentChecks_NotForPathChecks()
    {
        var config = RepoConfig.Parse("""
            {
              "scope": ["src/**"],
              "ignore": ["src/Docs.cs", { "paths": ["tests/**"], "checks": ["P005"] }]
            }
            """).Config;
        var files = new[]
        {
            PrTestData.Added("src/Docs.cs", "var pattern = \"::NAME::\";"),
            PrTestData.Added("src/Real.cs", "var key = \"::API_KEY::\";"),
            PrTestData.Added("tests/FixtureTests.cs", "var cs = \"Server=db;Password=Hunter2Prod;\";", "// TODO later"),
        };

        var findings = new PullRequestReviewer().Review(new PullRequestContext(files, config)).Findings;

        Assert.Equal(["src/Real.cs"], findings.Where(f => f.CheckId == "P001").Select(f => f.Path));
        Assert.DoesNotContain(findings, f => f.CheckId == "P005");
        Assert.Contains(findings, f => f.CheckId == "P002" && f.Path == "tests/FixtureTests.cs");
        Assert.Contains(findings, f => f.CheckId == "P004" && f.Path == "tests/FixtureTests.cs");
    }
}
