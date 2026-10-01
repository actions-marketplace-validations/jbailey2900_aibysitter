using Aibysitter.Rules.PullRequests;

namespace Aibysitter.Rules.Tests.PullRequests;

public class GlobTests
{
    [Theory]
    [InlineData("src/**", "src/A.cs", true)]
    [InlineData("src/**", "src/a/b/C.cs", true)]
    [InlineData("src/**", "src", true)]
    [InlineData("src/**", "srcx/A.cs", false)]
    [InlineData("src/**", "tests/A.cs", false)]
    [InlineData("**/*.md", "README.md", true)]
    [InlineData("**/*.md", "docs/a/b.md", true)]
    [InlineData("**/*.md", "docs/a/b.cs", false)]
    [InlineData("src/*.cs", "src/A.cs", true)]
    [InlineData("src/*.cs", "src/a/B.cs", false)]
    [InlineData("src/**/Tests/*.cs", "src/Tests/A.cs", true)]
    [InlineData("src/**/Tests/*.cs", "src/x/y/Tests/A.cs", true)]
    [InlineData("src/?.cs", "src/A.cs", true)]
    [InlineData("src/?.cs", "src/AB.cs", false)]
    [InlineData("/src/**", "src/A.cs", true)]
    [InlineData("docs/a.b", "docs/aXb", false)]
    [InlineData("Src/**", "src/A.cs", false)]
    [InlineData("src/**.cs", "src/a/B.cs", true)]
    public void IsMatch(string pattern, string path, bool expected)
    {
        Assert.Equal(expected, new Glob(pattern).IsMatch(path));
    }
}
