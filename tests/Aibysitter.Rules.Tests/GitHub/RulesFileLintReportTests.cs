using Aibysitter.Rules.PullRequests;
using Aibysitter.Web.GitHub;
using static Aibysitter.Rules.Tests.PullRequests.PrTestData;

namespace Aibysitter.Rules.Tests.GitHub;

public class RulesFileLintReportTests
{
    private static readonly PullRequestReviewer Reviewer = new();

    private static CheckRunReport Build(string configJson, params ChangedFile[] files)
    {
        var (config, errors) = RepoConfig.Parse(configJson);
        return CheckRunReport.Build(Reviewer.Review(new PullRequestContext(files, config)), Reviewer.Checks, files, config, errors);
    }

    [Fact]
    public void Annotations_UseRuleSeverity_SummaryShowsPerRule()
    {
        var report = Build("", Added("CLAUDE.md", "- Run:", "```", "dotnet test", "- Handle errors properly."));

        Assert.Contains(report.Annotations, a => a.Title == "P014 Rules file lint" && a.Severity == Severity.Error && a.Message.StartsWith("R012"));
        Assert.Contains("| P014 Rules file lint | Per rule |", report.Summary);
        Assert.Contains("(1 error,", report.Title);
    }

    [Fact]
    public void DisabledRules_ListedInSummary()
    {
        var report = Build("""{ "disable": ["R013", "R007"] }""", Added("CLAUDE.md", "- Use tabs."));

        Assert.Contains("Rules disabled for P014: R007, R013.", report.Summary);
    }

    [Fact]
    public void P014Disabled_NoRuleList()
    {
        var report = Build("""{ "disable": ["P014", "R013"] }""", Added("CLAUDE.md", "- Use tabs."));

        Assert.Contains("| P014 Rules file lint | Per rule | disabled |", report.Summary);
        Assert.DoesNotContain("Rules disabled for P014", report.Summary);
    }
}
