using Aibysitter.Rules.Rules;

namespace Aibysitter.Rules.Tests;

public class HedgedInstructionsTests
{
    private readonly HedgedInstructions _rule = new();

    private IReadOnlyList<Finding> Lint(string text) => _rule.Evaluate(RulesFile.Parse(text)).ToList();

    [Theory]
    [InlineData("- Try to keep functions short.", "try to")]
    [InlineData("- Use mocks if possible.", "if possible")]
    [InlineData("- Ideally, add a test.", "ideally")]
    [InlineData("- Use `import type` where possible.", "where possible")]
    [InlineData("- Keep state local when possible.", "when possible")]
    [InlineData("- Run headless whenever possible.", "whenever possible")]
    [InlineData("- Consider caching the result.", "consider")]
    [InlineData("You can also consider writing to a local file.", "consider")]
    [InlineData("- Prefer to run targeted tests.", "prefer to")]
    [InlineData("- Use lazy loading where appropriate.", "where appropriate")]
    [InlineData("- Add changelog entries when appropriate.", "when appropriate")]
    [InlineData("- Include the issue number if appropriate.", "if appropriate")]
    [InlineData("- Add to the preview flags (as appropriate).", "as appropriate")]
    public void Flags_Hedge(string line, string hedge)
    {
        Assert.Equal($"Hedge: \"{hedge}\"", Assert.Single(Lint(line)).Message);
    }

    [Theory]
    [InlineData("- Don't try to work around signing.")]
    [InlineData("- Never move it, or `npm i` would try to fetch the package.")]
    [InlineData("- Prefer to prepend to lists `[new | list]` not `list ++ [new]`.")]
    [InlineData("- Isolating design's contribution where possible")]
    [InlineData("- Do NOT try to bypass the limit.")]
    [InlineData("- Never try to sanitize the list.")]
    [InlineData("- Every new tool must consider the undo flow.")]
    [InlineData("- Consider whether the change is necessary.")]
    [InlineData("- Write \"Use X\", not \"You should consider X\".")]
    [InlineData("- Drivers use batch operations where possible.")]
    [InlineData("- Handles parallel execution where possible.")]
    [InlineData("| Ideally | Fallback |")]
    [InlineData("- Run `try to-parse` first.")]
    [InlineData("- Prefer records over classes.")]
    public void Skips_NonHedges(string line)
    {
        Assert.Empty(Lint(line));
    }

    [Fact]
    public void CodeBlock_Skipped()
    {
        Assert.Empty(Lint("```\n// try to connect\n```\n"));
    }

    [Fact]
    public void SeveralHedgesOnOneLine_OneFinding()
    {
        Assert.Equal("Hedge: \"try to\", \"where possible\"", Assert.Single(Lint("- Try to batch writes where possible.")).Message);
    }
}
