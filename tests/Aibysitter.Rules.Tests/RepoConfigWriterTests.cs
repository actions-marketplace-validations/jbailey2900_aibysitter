using Aibysitter.Rules.PullRequests;
using Aibysitter.Web.Config;

namespace Aibysitter.Rules.Tests;

public class RepoConfigWriterTests
{
    [Fact]
    public void Defaults_WriteConclusionOnly()
    {
        Assert.Equal("{\n  \"conclusion\": \"advisory\"\n}\n", RepoConfigWriter.Write([], ConclusionMode.Advisory, []));
    }

    [Fact]
    public void AllKeys_InOrder_TwoSpaceIndent()
    {
        var json = RepoConfigWriter.Write(["src/**", "tests/**"], ConclusionMode.FailOnErrors, ["P002", "R004"]);

        Assert.Equal(
            "{\n  \"scope\": [\n    \"src/**\",\n    \"tests/**\"\n  ],\n  \"conclusion\": \"fail-on-errors\",\n  \"disable\": [\n    \"P002\",\n    \"R004\"\n  ]\n}\n",
            json);
    }

    [Fact]
    public void Output_RoundTripsThroughAppParser()
    {
        var json = RepoConfigWriter.Write(["src/**", "docs/*.md"], ConclusionMode.FailOnErrors, ["P002", "R006"]);

        var (config, errors) = RepoConfig.Parse(json);

        Assert.Empty(errors);
        Assert.Equal(["src/**", "docs/*.md"], config.Scope.Select(g => g.Pattern));
        Assert.Equal(ConclusionMode.FailOnErrors, config.Conclusion);
        Assert.Equal(["P002", "R006"], config.Disabled.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Glob_WithQuotesAndNonAscii_IsEscapedOnlyWhereJsonRequires()
    {
        var json = RepoConfigWriter.Write(["docs/\"x\"/é+*.md"], ConclusionMode.Advisory, []);

        Assert.Contains("\"docs/\\\"x\\\"/é+*.md\"", json);
        Assert.Equal("docs/\"x\"/é+*.md", RepoConfig.Parse(json).Config.Scope.Single().Pattern);
    }

    [Fact]
    public void ScopeLines_TrimsDropsBlanksAndRepeats_KeepsOrder()
    {
        Assert.Equal(["src/**", "tests/**"], RepoConfigWriter.ScopeLines("  src/**\r\n\n tests/** \nsrc/**\n"));
        Assert.Empty(RepoConfigWriter.ScopeLines(null));
    }

    [Fact]
    public void Comment_WrittenLast_OnlyWhenTrue()
    {
        Assert.Equal("{\n  \"conclusion\": \"advisory\",\n  \"comment\": true\n}\n", RepoConfigWriter.Write([], ConclusionMode.Advisory, [], comment: true));
        Assert.DoesNotContain("comment", RepoConfigWriter.Write([], ConclusionMode.Advisory, [], comment: false));
        Assert.True(RepoConfig.Parse(RepoConfigWriter.Write(["src/**"], ConclusionMode.Advisory, ["P002"], comment: true)).Config.Comment);
    }
}
