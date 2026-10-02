using Aibysitter.Rules.PullRequests;
using Aibysitter.Web.GitHub;
using static Aibysitter.Rules.Tests.PullRequests.PrTestData;

namespace Aibysitter.Rules.Tests.PullRequests;

public class ConfigValidationTests
{
    private static readonly PullRequestReviewer Reviewer = new();

    [Theory]
    [InlineData("src\\**", "use / as the path separator")]
    [InlineData("!docs/**", "! negation is not supported; list the paths to include")]
    [InlineData("{src,tests}/**", "{a,b} alternatives are not supported; list each pattern")]
    [InlineData("[Tt]ests/**", "[...] character classes are not supported")]
    [InlineData("src/", "a trailing / matches nothing; use src/**")]
    [InlineData("src//a", "empty path segment (//)")]
    [InlineData("./src/**", "paths start at the repository root; remove ./ and ../")]
    [InlineData("src/../x", "paths start at the repository root; remove ./ and ../")]
    [InlineData("src/**.cs", "** must be a whole path segment; for files in any folder use **/*.cs")]
    [InlineData("a**", "** must be a whole path segment; for files in any folder use **/*.cs")]
    [InlineData("src/***", "** must be a whole path segment; for files in any folder use **/*.cs")]
    public void Glob_Rejects(string pattern, string message)
    {
        Assert.False(Glob.TryCreate(pattern, out var glob, out var error));
        Assert.Null(glob);
        Assert.Equal(message, error);
        Assert.Equal(message, Assert.Throws<ArgumentException>(() => new Glob(pattern)).Message.Split(" (Parameter")[0]);
    }

    [Theory]
    [InlineData("**")]
    [InlineData("src/**")]
    [InlineData("**/*.cs")]
    [InlineData("docs/*.md")]
    [InlineData("a/?.txt")]
    [InlineData("/src/**")]
    [InlineData("src/**/bin/*.dll")]
    public void Glob_Accepts(string pattern)
    {
        Assert.True(Glob.TryCreate(pattern, out var glob, out var error));
        Assert.Null(error);
        Assert.NotNull(glob);
    }

    [Fact]
    public void Errors_CarryTheLineOfTheirKeyOrItem()
    {
        var (config, errors) = RepoConfig.Parse("""
            {
              // comment
              "scope": [
                "src/**",
                "src/**.cs",
                3
              ],
              "conclusion": "strict",
              "disable": ["P002", "P999"],
              "comment": "yes",
              "extra": 1,
            }
            """);

        Assert.Equal(
        [
            (5, ".github/aibysitter.json: scope entry \"src/**.cs\": ** must be a whole path segment; for files in any folder use **/*.cs"),
            (6, ".github/aibysitter.json: \"scope\" entries must be non-empty strings"),
            (8, ".github/aibysitter.json: \"conclusion\" must be \"advisory\" or \"fail-on-errors\""),
            (9, ".github/aibysitter.json: \"disable\" entry \"P999\" is not a known check or rule ID"),
            (10, ".github/aibysitter.json: \"comment\" must be true or false"),
            (11, ".github/aibysitter.json: unknown key \"extra\""),
        ], errors.Select(e => (e.Line, e.Message)));
        Assert.Equal(["src/**"], config.Scope.Select(g => g.Pattern));
    }

    [Theory]
    [InlineData("{\n  \"scope\": [\n", 3)]
    [InlineData("[]", 1)]
    public void InvalidJsonAndRoot_Lines(string json, int line)
    {
        Assert.Equal(line, Assert.Single(RepoConfig.Parse(json).Errors).Line);
    }

    private static CheckRunReport Report(string json, params ChangedFile[] files)
    {
        var (config, errors) = RepoConfig.Parse(json);
        return CheckRunReport.Build(Reviewer.Review(new PullRequestContext(files, config)), Reviewer.Checks, files, config, errors);
    }

    [Fact]
    public void Report_ConfigErrors_AnnotatedAsWarnings_NotFindings()
    {
        var report = Report("{\n  \"conclusion\": \"strict\"\n}", Added("src/A.cs", "public class A { }"));

        var annotation = Assert.Single(report.Annotations);
        Assert.Equal((RepoConfig.FilePath, 2, Severity.Warning, "Config error", true), (annotation.Path, annotation.Line, annotation.Severity, annotation.Title, annotation.IsConfigError));
        Assert.Equal("No findings; 1 config error", report.Title);
        Assert.Equal(ReviewConclusion.Success, report.Conclusion);
        Assert.Contains("- Line 2: .github/aibysitter.json: \"conclusion\" must be", report.Summary);
    }

    [Fact]
    public void Report_FailOnErrors_WithConfigError_Fails()
    {
        var report = Report("{\"conclusion\": \"fail-on-errors\", \"scope\": [\"src/\"]}", Added("src/A.cs", "public class A { }"));

        Assert.Equal(ReviewConclusion.Failure, report.Conclusion);
        Assert.Equal("No findings; 1 config error", report.Title);
        Assert.Contains("Config errors (defaults used for these; the check fails under fail-on-errors):", report.Summary);
    }

    [Fact]
    public void Report_Advisory_WithConfigError_Unchanged()
    {
        var report = Report("{\"scope\": [\"src/\"]}", Added("src/A.cs", "var k = ::KEY::;"));

        Assert.Equal(ReviewConclusion.Neutral, report.Conclusion);
        Assert.Equal("1 finding (1 error, 0 warnings); 1 config error", report.Title);
    }

    [Fact]
    public void Comment_LeavesConfigErrorsOutOfFindings()
    {
        var report = Report("{\"comment\": true, \"extra\": 1}", Added("src/A.cs", "var k = ::KEY::;"));

        var body = ReviewComment.Build(report, new PullRequestRef(1, "o", "r", 7, "abcdef0123"), 99);

        Assert.Single(body.Split('\n'), l => l.StartsWith("- [`", StringComparison.Ordinal));
        Assert.DoesNotContain("aibysitter.json:", body.Split("Findings:")[1]);
    }
}
