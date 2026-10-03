using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests.Cli;

public sealed class CliFixTests : IDisposable
{
    private const string Broken = "# Rules\n- Run tests before commit.\n- run tests before commit.\n## Testing\n## Style\n- Use tabs.\nRun:\n```\ndotnet test\n";
    private const string Fixed = "# Rules\n- Run tests before commit.\n- run tests before commit.\n## Style\n- Use tabs.\nRun:\n```\ndotnet test\n```\n";

    private readonly string dir = Directory.CreateTempSubdirectory("aibysitter-fix-").FullName;

    public void Dispose() => Directory.Delete(dir, recursive: true);

    private string Write(string name, string content)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes(content));
        return path;
    }

    [Fact]
    public void InPlace_WritesFile_PrintsSummary()
    {
        var path = Write("CLAUDE.md", Broken);

        var result = CliTests.Run(["fix", path]);

        Assert.Equal(0, result.Exit);
        Assert.Equal(Fixed, File.ReadAllText(path));
        Assert.Equal($"{path}: fixed 2 (R011 ×1, R012 ×1); score 82 → 96.\n", result.Out);
        Assert.Equal($"{path}: nothing to fix.\n", CliTests.Run(["fix", path]).Out);
    }

    [Fact]
    public void DryRun_PrintsDiff_WritesNothing_Exit1()
    {
        var path = Write("CLAUDE.md", Broken);

        var result = CliTests.Run(["fix", "--dry-run", path]);

        Assert.Equal(1, result.Exit);
        Assert.Equal(Broken, File.ReadAllText(path));
        Assert.Equal(
            $"--- a/{path}\n+++ b/{path}\n"
            + "@@ -1,9 +1,9 @@\n # Rules\n - Run tests before commit.\n - run tests before commit.\n-## Testing\n ## Style\n - Use tabs.\n Run:\n ```\n dotnet test\n+```\n"
            + $"{path}: would fix 2 (R011 ×1, R012 ×1); score 82 → 96.\n",
            result.Out);
    }

    [Fact]
    public void DryRun_Clean_Exit0_NoDiff()
    {
        var result = CliTests.Run(["fix", "--dry-run", Write("CLAUDE.md", Fixed)]);

        Assert.Equal(0, result.Exit);
        Assert.EndsWith(": nothing to fix.\n", result.Out);
        Assert.DoesNotContain("---", result.Out);
    }

    [Fact]
    public void Diff_SeparateHunks_WhenChangesAreFarApart()
    {
        var middle = string.Concat(Enumerable.Range(1, 10).Select(i => $"- Rule number {i} applies to every file.\n"));
        var text = "# Rules\n## Empty\n## Body\n" + middle + "## Tail\n";

        var result = CliTests.Run(["fix", "-", "--dry-run", "--stdin-path", "CLAUDE.md"], text);

        Assert.Equal(2, result.Out.Split("\n@@ ").Length - 1 + (result.Out.StartsWith("@@", StringComparison.Ordinal) ? 1 : 0));
        Assert.Contains("@@ -1,5 +1,4 @@\n # Rules\n-## Empty\n ## Body\n", result.Out);
        Assert.Contains("@@ -11,4 +10,3 @@\n", result.Out);
    }

    [Fact]
    public void Stdin_FixedTextToStdout_SummaryToStderr()
    {
        var result = CliTests.Run(["fix", "-", "--stdin-path", "docs/CLAUDE.md"], Broken);

        Assert.Equal(0, result.Exit);
        Assert.Equal(Fixed, result.Out);
        Assert.Equal("docs/CLAUDE.md: fixed 2 (R011 ×1, R012 ×1); score 82 → 96.\n", result.Err);
    }

    [Fact]
    public void Disable_KeepsThatRule()
    {
        var result = CliTests.Run(["fix", "-", "--disable", "R012"], Broken);

        Assert.Equal(Broken.Replace("## Testing\n", ""), result.Out);
    }

    [Theory]
    [InlineData(new[] { "fix" }, "fix needs a file path, or - to read standard input.")]
    [InlineData(new[] { "fix", "a.md", "b.md" }, "fix takes one file; got \"a.md\" and \"b.md\".")]
    [InlineData(new[] { "fix", "-", "--json" }, "Unknown option \"--json\".")]
    [InlineData(new[] { "fix", "a.md", "--stdin-path", "CLAUDE.md" }, "--stdin-path is for standard input; use - as the file.")]
    [InlineData(new[] { "fix", "-", "--disable", "R999" }, "Not a lint rule ID: R999.")]
    public void UsageErrors(string[] args, string message)
    {
        var result = CliTests.Run(args);

        Assert.Equal(2, result.Exit);
        Assert.Contains(message, result.Err);
    }

    [Fact]
    public void MissingFile_Exit3()
    {
        var result = CliTests.Run(["fix", Path.Combine(dir, "nope.md")]);

        Assert.Equal(3, result.Exit);
        Assert.Contains("not found", result.Err);
    }
}

public class FixableRulePageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("R011", "deletes the empty heading and the blank lines after it.")]
    [InlineData("R012", "adds a matching closing fence at the end of the file.")]
    public async Task FixableRules_SayHow(string id, string text)
    {
        var html = await factory.CreateClient().GetStringAsync($"/Rules/{id}");

        Assert.Contains("<dt>Fixable</dt>", html);
        Assert.Contains(text, html);
    }

    [Fact]
    public async Task OtherRules_NotFixable()
    {
        Assert.DoesNotContain("<dt>Fixable</dt>", await factory.CreateClient().GetStringAsync("/Rules/R002"));
    }

    [Fact]
    public async Task R005_NotFixable()
    {
        var html = await factory.CreateClient().GetStringAsync("/Rules/R005");

        Assert.DoesNotContain("<dt>Fixable</dt>", html);
    }
}
