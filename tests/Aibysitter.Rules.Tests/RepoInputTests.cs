using Aibysitter.Web.Linting;

namespace Aibysitter.Rules.Tests;

public class RepoInputTests
{
    [Theory]
    [InlineData("gaearon/overreacted.io", "gaearon", "overreacted.io", false)]
    [InlineData("  oven-sh/bun  ", "oven-sh", "bun", false)]
    [InlineData("github.com/oven-sh/bun", "oven-sh", "bun", false)]
    [InlineData("www.github.com/oven-sh/bun", "oven-sh", "bun", false)]
    [InlineData("https://github.com/oven-sh/bun", "oven-sh", "bun", false)]
    [InlineData("http://github.com/oven-sh/bun/", "oven-sh", "bun", false)]
    [InlineData("https://github.com/oven-sh/bun.git", "oven-sh", "bun", false)]
    [InlineData("https://github.com/oven-sh/bun?tab=readme-ov-file#x", "oven-sh", "bun", false)]
    [InlineData("https://github.com/oven-sh/bun/tree/canary/src", "oven-sh", "bun", true)]
    [InlineData("https://github.com/mozilla/pdf.js/blob/master/AGENTS.md", "mozilla", "pdf.js", true)]
    [InlineData("https://GitHub.com/A1/b_c", "A1", "b_c", false)]
    public void Accepts(string input, string owner, string repo, bool extraPath)
    {
        Assert.True(RepoInput.TryParse(input, out var parsed));
        Assert.Equal(new RepoRef(owner, repo, extraPath), parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bun")]
    [InlineData("oven-sh/bun/extra")]
    [InlineData("oven-sh/bun?x=1")]
    [InlineData("../etc/passwd")]
    [InlineData("oven-sh/..")]
    [InlineData("oven-sh/.")]
    [InlineData("%2e%2e/bun")]
    [InlineData("oven-sh/%2e%2e")]
    [InlineData("-bad/bun")]
    [InlineData("toolongowner-toolongowner-toolongowner12/x")]
    [InlineData("oven sh/bun")]
    [InlineData("https://gitlab.com/oven-sh/bun")]
    [InlineData("https://raw.githubusercontent.com/oven-sh/bun/HEAD/CLAUDE.md")]
    [InlineData("https://github.com.evil.example/oven-sh/bun")]
    [InlineData("https://user@github.com/oven-sh/bun")]
    [InlineData("https://github.com:8443/oven-sh/bun")]
    [InlineData("ftp://github.com/oven-sh/bun")]
    [InlineData("https://github.com/oven-sh")]
    [InlineData("https://github.com/oven-sh/%2e%2e/x")]
    [InlineData("file:///etc/passwd")]
    public void Rejects(string? input)
    {
        Assert.False(RepoInput.TryParse(input, out var parsed));
        Assert.Null(parsed);
    }
}
