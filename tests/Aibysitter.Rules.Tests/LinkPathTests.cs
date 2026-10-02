using Aibysitter.Web.Linting;

namespace Aibysitter.Rules.Tests;

public class LinkPathTests
{
    [Theory]
    [InlineData("CLAUDE.md", ".ai/AGENTS.md", ".ai/AGENTS.md")]
    [InlineData("CLAUDE.md", "AGENTS.md\n", "AGENTS.md")]
    [InlineData("CLAUDE.md", "./docs/rules.md", "docs/rules.md")]
    [InlineData(".github/copilot-instructions.md", "../AGENTS.md", "AGENTS.md")]
    [InlineData(".github/copilot-instructions.md", "instructions/main.md", ".github/instructions/main.md")]
    [InlineData("CLAUDE.md", "docs/rules", "docs/rules")]
    public void ResolvesRelativeToLinkingFile(string from, string content, string expected)
    {
        Assert.True(LinkPath.TryResolve(from, content, out var target));
        Assert.Equal(expected, target);
    }

    [Theory]
    [InlineData("CLAUDE.md", "# Rules\n- Use tabs.")]
    [InlineData("CLAUDE.md", "TODO")]
    [InlineData("CLAUDE.md", "AGENTS.md is the source of truth")]
    [InlineData("CLAUDE.md", "../outside.md")]
    [InlineData(".github/copilot-instructions.md", "../../outside.md")]
    [InlineData("CLAUDE.md", "/etc/passwd")]
    [InlineData("CLAUDE.md", "https://example.com/x.md")]
    [InlineData("CLAUDE.md", "a/b.md\nc/d.md")]
    [InlineData("CLAUDE.md", "./")]
    [InlineData("CLAUDE.md", "")]
    public void NotALink(string from, string content)
    {
        Assert.False(LinkPath.TryResolve(from, content, out _));
    }
}
