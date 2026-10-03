using System.Text.RegularExpressions;
using Aibysitter.Rules.Tests.Parity;

namespace Aibysitter.Rules.Tests;

public class ReleaseWorkflowTests
{
    private static string WorkflowsDir => Path.Combine(NodeRunner.RepoRoot, ".github", "workflows");

    private static string ReleaseCli => File.ReadAllText(Path.Combine(WorkflowsDir, "release-cli.yml"));

    [Fact]
    public void ReleaseCli_RunsOnCliTagsOnly()
    {
        Assert.Contains("on:\n  push:\n    tags: [\"cli-v*\"]\n", ReleaseCli, StringComparison.Ordinal);
        Assert.DoesNotContain("pull_request", ReleaseCli, StringComparison.Ordinal);
        Assert.DoesNotContain("branches:", ReleaseCli, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseCli_ReadOnlyToken() =>
        Assert.Contains("permissions:\n  contents: read\n\n", ReleaseCli, StringComparison.Ordinal);

    [Fact]
    public void ReleaseCli_ChecksTagAgainstMainAndVersion()
    {
        Assert.Contains("git merge-base --is-ancestor \"$GITHUB_SHA\" origin/main", ReleaseCli, StringComparison.Ordinal);
        Assert.Contains("[ \"cli-v$version\" = \"$GITHUB_REF_NAME\" ]", ReleaseCli, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseCli_PushesOnlyWhenPublic_WithApiKeySecret()
    {
        var push = Regex.Match(ReleaseCli, @"- name: Push to nuget\.org\n(?<body>(?:        .*\n)+)").Groups["body"].Value;

        Assert.Contains("if: ${{ !github.event.repository.private }}", push, StringComparison.Ordinal);
        Assert.Contains("NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}", push, StringComparison.Ordinal);
        Assert.Contains("--api-key \"$NUGET_API_KEY\"", push, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(ReleaseCli, @"secrets\."));
    }

    [Theory]
    [InlineData("action-test.yml")]
    [InlineData("ci.yml")]
    [InlineData("deploy-scripts.yml")]
    [InlineData("deploy.yml")]
    [InlineData("release-cli.yml")]
    public void Workflow_PinsEveryActionBySha(string file)
    {
        var uses = Regex.Matches(File.ReadAllText(Path.Combine(WorkflowsDir, file)), @"uses:\s*(?<ref>\S+)")
            .Select(m => m.Groups["ref"].Value)
            .Where(r => !r.StartsWith("./", StringComparison.Ordinal))
            .ToList();

        Assert.All(uses, r => Assert.Matches(@"@[0-9a-f]{40}$", r));
    }

    [Fact]
    public void WorkflowList_IsCovered() =>
        Assert.Equal(
            ["action-test.yml", "ci.yml", "deploy-scripts.yml", "deploy.yml", "release-cli.yml"],
            Directory.GetFiles(WorkflowsDir, "*.yml").Select(Path.GetFileName).Order(StringComparer.Ordinal));
}
