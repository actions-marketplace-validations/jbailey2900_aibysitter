using Aibysitter.Rules.PullRequests;

namespace Aibysitter.Rules.Tests.PullRequests;

public class RepoConfigTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingConfig_IsDefault_NoErrors(string? json)
    {
        var (config, errors) = RepoConfig.Parse(json);

        Assert.Same(RepoConfig.Default, config);
        Assert.Empty(errors);
        Assert.False(config.HasScope);
        Assert.Equal(ConclusionMode.Advisory, config.Conclusion);
    }

    [Fact]
    public void ValidConfig_Parses()
    {
        var (config, errors) = RepoConfig.Parse("""
            {
              // comments allowed
              "scope": ["src/**", "tests/**"],
              "conclusion": "fail-on-errors",
            }
            """);

        Assert.Empty(errors);
        Assert.Equal(new[] { "src/**", "tests/**" }, config.Scope.Select(g => g.Pattern));
        Assert.Equal(ConclusionMode.FailOnErrors, config.Conclusion);
        Assert.True(config.InScope("tests/A.cs"));
        Assert.False(config.InScope("docs/A.md"));
    }

    [Fact]
    public void InvalidJson_FallsBackToDefault_WithError()
    {
        var (config, errors) = RepoConfig.Parse("{ \"scope\": [ ");

        Assert.Same(RepoConfig.Default, config);
        Assert.StartsWith(".github/aibysitter.json: invalid JSON", Assert.Single(errors));
    }

    [Theory]
    [InlineData("[]", "root must be an object")]
    [InlineData("{\"scope\": \"src/**\"}", "\"scope\" must be an array")]
    [InlineData("{\"scope\": [\"\", 3]}", "\"scope\" entries must be non-empty strings")]
    [InlineData("{\"conclusion\": \"strict\"}", "\"conclusion\" must be")]
    [InlineData("{\"conclusion\": true}", "\"conclusion\" must be")]
    [InlineData("{\"scopes\": []}", "unknown key \"scopes\"")]
    [InlineData("{\"disable\": \"P002\"}", "\"disable\" must be an array of check IDs")]
    [InlineData("{\"disable\": [\"P999\"]}", "\"disable\" entry \"P999\" is not a known check ID")]
    [InlineData("{\"disable\": [\"R002\"]}", "\"disable\" entry \"R002\" is not a known check ID")]
    [InlineData("{\"disable\": [2]}", "\"disable\" entry 2 is not a known check ID")]
    public void InvalidParts_ReportErrors(string json, string expected)
    {
        var (_, errors) = RepoConfig.Parse(json);

        Assert.Contains(errors, e => e.Contains(expected));
    }

    [Fact]
    public void BadConclusion_KeepsValidScope()
    {
        var (config, errors) = RepoConfig.Parse("{\"scope\": [\"src/**\"], \"conclusion\": \"strict\"}");

        Assert.Single(errors);
        Assert.True(config.HasScope);
        Assert.Equal(ConclusionMode.Advisory, config.Conclusion);
    }

    [Fact]
    public void Disable_ParsesKnownIds_CaseInsensitive()
    {
        var (config, errors) = RepoConfig.Parse("""{ "disable": ["P002", " p001 ", "P999"] }""");

        Assert.Equal(new[] { "P001", "P002" }, config.Disabled.Order());
        Assert.False(config.IsEnabled("P002"));
        Assert.True(config.IsEnabled("P003"));
        Assert.Single(errors);
    }

    [Fact]
    public void Default_DisablesNothing()
    {
        Assert.Empty(RepoConfig.Default.Disabled);
    }
}
