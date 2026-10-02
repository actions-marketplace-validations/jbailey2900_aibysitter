using Aibysitter.Cli;
using Aibysitter.Packs;

namespace Aibysitter.Rules.Tests.Cli;

/// <summary>init and packs. Paths are absolute under a temp folder, so the working directory is not changed.</summary>
public sealed class CliInitTests : IDisposable
{
    private readonly string dir = Directory.CreateTempSubdirectory("aibysitter-init-").FullName;

    public void Dispose() => Directory.Delete(dir, recursive: true);

    private string P(string relative) => Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void Packs_ListsIdsTitlesAndShortTargets()
    {
        var result = CliTests.Run(["packs"]);

        Assert.Equal(CliApp.Ok, result.Exit);
        var lines = result.Out.TrimEnd('\n').Split('\n');
        Assert.Equal(new PackCatalog().All.Count, lines.Length);
        Assert.Contains("aspnet-web-api       ASP.NET Core web API (claude, agents, gemini, copilot, cursor, cursorrules, windsurf)", lines);
    }

    [Fact]
    public void Init_WritesComposedFile_PrintsScore()
    {
        var path = P("CLAUDE.md");
        var result = CliTests.Run(["init", "--packs", "starter", "--format", "claude", "--output", path]);

        Assert.Equal(CliApp.Ok, result.Exit);
        Assert.Equal(PackComposer.Compose([new PackCatalog().Find("starter")!], RulesFormat.ClaudeMd), File.ReadAllText(path));
        Assert.Matches($@"^Wrote {System.Text.RegularExpressions.Regex.Escape(path)} \(starter\): \d+/100 [A-F] \(CLAUDE\.md, ruleset v{RulesetVersion.Current}\)\n$", result.Out);
    }

    [Fact]
    public void Init_TwoPacks_TitleAndCursorMdc_CreatesFolders_LintsSameAsLintCommand()
    {
        var path = P("repo/.cursor/rules/app.mdc");
        var result = CliTests.Run(["init", "--packs=dotnet-razor-pages,aspnet-web-api", "--format=cursor", "--title", "Orders portal", "--output", path]);
        var text = File.ReadAllText(path);
        var lint = CliTests.Run(["lint", path]);

        Assert.Equal(CliApp.Ok, result.Exit);
        Assert.StartsWith("---\ndescription: Orders portal\nalwaysApply: true\n---\n\n# Orders portal\n", text);
        Assert.Equal(lint.Out.Split('\n')[0].Split(": ", 2)[1], result.Out.TrimEnd('\n').Split("): ", 2)[1]);
        Assert.Contains("(Cursor rule (.mdc), ruleset", result.Out);
    }

    [Fact]
    public void Init_ExistingFile_RefusedWithoutForce_OverwrittenWithForce()
    {
        var path = P("AGENTS.md");
        File.WriteAllText(path, "keep");

        var refused = CliTests.Run(["init", "--packs", "monorepo", "--format", "agents", "--output", path]);
        Assert.Equal(CliApp.FileError, refused.Exit);
        Assert.Equal($"aibysitter: {path} exists; use --force to overwrite it.\n", refused.Err);
        Assert.Equal("keep", File.ReadAllText(path));

        var forced = CliTests.Run(["init", "--packs", "monorepo", "--format", "agents", "--output", path, "--force"]);
        Assert.Equal(CliApp.Ok, forced.Exit);
        Assert.StartsWith("# Project rules\n", File.ReadAllText(path));
    }

    [Fact]
    public void Init_OutputIsDirectory_FileError()
    {
        var result = CliTests.Run(["init", "--packs", "starter", "--format", "claude", "--output", dir, "--force"]);

        Assert.Equal(CliApp.FileError, result.Exit);
        Assert.StartsWith($"aibysitter: cannot write {dir}: ", result.Err);
    }

    [Theory]
    [InlineData(new[] { "init" }, "init needs --packs. Run 'aibysitter packs' to list them.")]
    [InlineData(new[] { "init", "--packs", "starter" }, "init needs --format: claude, agents, gemini, copilot, cursor, cursorrules, windsurf.")]
    [InlineData(new[] { "init", "--packs", "starter", "--format", "word" }, "--format needs one of:")]
    [InlineData(new[] { "init", "--packs", "starter", "--format", "markdown" }, "--format needs one of:")]
    [InlineData(new[] { "init", "--packs", "nope,starter,zzz", "--format", "claude" }, "Unknown pack: nope, zzz. Run 'aibysitter packs' to list them.")]
    [InlineData(new[] { "init", "--packs", "", "--format", "claude" }, "--packs needs pack IDs")]
    [InlineData(new[] { "init", "--packs", "starter", "--format", "claude", "--title", " " }, "--title needs text.")]
    [InlineData(new[] { "init", "--packs", "starter", "--format", "claude", "extra" }, "init takes options only; got \"extra\".")]
    [InlineData(new[] { "init", "--packs", "starter", "--format", "claude", "--bogus" }, "Unknown option \"--bogus\".")]
    [InlineData(new[] { "packs", "x" }, "packs takes no arguments; got \"x\".")]
    [InlineData(new[] { "init", "--packs", "monorepo,starter", "--format", "claude" }, "starter is a standalone pack; use it on its own.")]
    public void UsageErrors_Exit2(string[] args, string message)
    {
        var result = CliTests.Run(args);

        Assert.Equal(CliApp.UsageError, result.Exit);
        Assert.StartsWith("aibysitter: " + message, result.Err);
        Assert.Empty(Directory.GetFileSystemEntries(dir));
    }
}
