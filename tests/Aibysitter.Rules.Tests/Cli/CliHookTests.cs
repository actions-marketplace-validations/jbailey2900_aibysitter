using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Aibysitter.Cli;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests.Cli;

[Trait("Category", "Hooks")]
public sealed class CliHookTests : IDisposable
{
    internal const string Contradiction = CliTests.Contradiction;
    internal const string InfoOnly = "# Rules\n- Use tabs because tabs keep the diff small.\n";

    private readonly string dir = Directory.CreateTempSubdirectory("aibysitter-hook-").FullName;

    public void Dispose() => Directory.Delete(dir, recursive: true);

    private string Write(string relative, string content)
    {
        var path = Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static string Input(string filePath, string? cwd) =>
        JsonSerializer.Serialize(new { session_id = "s", hook_event_name = "PostToolUse", tool_name = "Edit", tool_input = new { file_path = filePath }, cwd });

    internal static (int Exit, string Err) Hook(string stdin, string[]? args = null, string? env = null)
    {
        var stderr = new StringWriter { NewLine = "\n" };
        var exit = HookCommand.ClaudeCode(args ?? [], new StringReader(stdin), stderr, name => name == HookCommand.ArgsVariable ? env : null);
        return (exit, stderr.ToString());
    }

    [Fact]
    public void RulesFileWithError_Exit2_FindingsOnStderr_RelativeToCwd()
    {
        var path = Write("CLAUDE.md", Contradiction);

        var (exit, err) = Hook(Input(path, dir));

        Assert.Equal(2, exit);
        Assert.Equal(
            "aibysitter: CLAUDE.md has 1 finding to fix (score 90/100 A):\n"
            + "CLAUDE.md:3 R003 Error: Contradicts line 2: \"use tabs\" is both required and forbidden. Fix: Keep one instruction and delete the other.\n",
            err);
    }

    [Fact]
    public void RelativeFilePath_ResolvedAgainstCwd()
    {
        Write("docs/AGENTS.md", Contradiction);

        var (exit, err) = Hook(Input("docs/AGENTS.md", dir));

        Assert.Equal(2, exit);
        Assert.StartsWith("aibysitter: docs/AGENTS.md has 1 finding", err);
    }

    [Theory]
    [InlineData("notes.md", Contradiction)]
    [InlineData("CLAUDE.md", "# Rules\n- Use tabs.\n")]
    [InlineData("CLAUDE.md", InfoOnly)]
    public void NotARulesFile_Clean_OrInfoOnly_Exit0_Silent(string name, string content)
    {
        var (exit, err) = Hook(Input(Write(name, content), dir));

        Assert.Equal((0, ""), (exit, err));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"tool_input\": {\"command\": \"ls\"}}")]
    [InlineData("{\"tool_input\": {\"file_path\": 3}}")]
    public void MalformedOrOtherToolInput_Exit0(string stdin)
    {
        Assert.Equal(0, Hook(stdin).Exit);
    }

    [Fact]
    public void MissingFile_Exit0()
    {
        Assert.Equal(0, Hook(Input(Path.Combine(dir, "CLAUDE.md"), dir)).Exit);
    }

    [Fact]
    public void Disable_FromArgsOrEnvironment()
    {
        var input = Input(Write("CLAUDE.md", Contradiction), dir);

        Assert.Equal(0, Hook(input, ["--disable", "R003"]).Exit);
        Assert.Equal(0, Hook(input, env: "--disable=R003").Exit);
        var (exit, err) = Hook(input, ["--json"]);
        Assert.Equal(2, exit);
        Assert.StartsWith("aibysitter hook: ignored argument \"--json\"; only --disable is supported.\n", err);
    }

    [Fact]
    public void Dispatch_ThroughCliApp()
    {
        Assert.Equal(0, CliTests.Run(["hook", "claude-code"], "{}").Exit);
        var unknown = CliTests.Run(["hook", "cursor"]);
        Assert.Equal(2, unknown.Exit);
        Assert.Contains("hook needs a name: claude-code.", unknown.Err);
    }

    [Fact]
    public void StdinPath_SetsFormatAndLabel()
    {
        var result = CliTests.Run(["lint", "-", "--stdin-path", ".cursor/rules/style.mdc", "--json"], "# Style\n- Use tabs.\n");

        using var json = JsonDocument.Parse(result.Out);
        Assert.Equal(".cursor/rules/style.mdc", json.RootElement.GetProperty("file").GetString());
        Assert.Equal("CursorMdc", json.RootElement.GetProperty("detectedFormat").GetString());
        Assert.StartsWith("docs/CLAUDE.md: ", CliTests.Run(["lint", "-", "--stdin-path", "docs/CLAUDE.md"], "# R\n- Use tabs.\n").Out);
    }

    [Theory]
    [InlineData(new[] { "lint", "x.md", "--stdin-path", "CLAUDE.md" }, "--stdin-path is for standard input; use - as the file.")]
    [InlineData(new[] { "lint", "-", "--stdin-path" }, "--stdin-path needs a path, for example --stdin-path CLAUDE.md.")]
    public void StdinPath_UsageErrors(string[] args, string message)
    {
        var result = CliTests.Run(args);

        Assert.Equal(2, result.Exit);
        Assert.Contains(message, result.Err);
    }
}

/// <summary>Runs hooks/pre-commit under sh in a throwaway git repository, with the built CLI as <c>aibysitter</c>.</summary>
[Trait("Category", "Hooks")]
public sealed class PreCommitScriptTests : IDisposable
{
    private readonly string repo = Directory.CreateTempSubdirectory("aibysitter-precommit-").FullName;
    private readonly string bin = Directory.CreateTempSubdirectory("aibysitter-bin-").FullName;

    public PreCommitScriptTests()
    {
        var wrapper = Path.Combine(bin, "aibysitter");
        File.WriteAllText(wrapper, $"#!/bin/sh\nexec dotnet \"{Slash(CliDll())}\" \"$@\"\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(wrapper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Git("init", "-q");
    }

    public void Dispose()
    {
        foreach (var d in new[] { repo, bin })
        {
            foreach (var f in Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(f, FileAttributes.Normal);
            }

            Directory.Delete(d, recursive: true);
        }
    }

    private static string Slash(string path) => path.Replace('\\', '/');

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "Aibysitter.slnx")))
        {
            dir = dir.Parent;
        }

        return dir.FullName;
    }

    private static string CliDll()
    {
        var config = AppContext.BaseDirectory.Replace('\\', '/').Contains("/Release/", StringComparison.Ordinal) ? "Release" : "Debug";
        return Path.Combine(RepoRoot(), "src", "Aibysitter.Cli", "bin", config, "net10.0", "Aibysitter.Cli.dll");
    }

    /// <summary>Git for Windows' sh, not WSL's; plain sh elsewhere.</summary>
    private static string Shell()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "sh";
        }

        var git = Environment.GetEnvironmentVariable("PATH")!.Split(Path.PathSeparator)
            .Select(p => Path.Combine(p, "git.exe")).First(File.Exists);
        var root = Directory.GetParent(Path.GetDirectoryName(git)!)!.FullName;
        return new[] { Path.Combine(root, "bin", "sh.exe"), Path.Combine(root, "usr", "bin", "sh.exe") }.First(File.Exists);
    }

    private (int Exit, string Out, string Err) Run(string file, IEnumerable<string> args, IDictionary<string, string>? env = null)
    {
        var info = new ProcessStartInfo(file) { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args)
        {
            info.ArgumentList.Add(a);
        }

        info.Environment["PATH"] = bin + Path.PathSeparator + info.Environment["PATH"];
        info.Environment["GIT_AUTHOR_NAME"] = info.Environment["GIT_COMMITTER_NAME"] = "t";
        info.Environment["GIT_AUTHOR_EMAIL"] = info.Environment["GIT_COMMITTER_EMAIL"] = "t@example.com";
        info.Environment.Remove("AIBYSITTER_HOOK_ARGS");
        foreach (var (k, v) in env ?? new Dictionary<string, string>())
        {
            info.Environment[k] = v;
        }

        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.Result.Replace("\r\n", "\n"), stderr.Result.Replace("\r\n", "\n"));
    }

    private void Git(params string[] args) => Assert.Equal(0, Run("git", ["-c", "core.autocrlf=false", .. args]).Exit);

    private void Stage(string path, string content)
    {
        var full = Path.Combine(repo, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        Git("add", "--", path);
    }

    private (int Exit, string Out, string Err) Hook(IDictionary<string, string>? env = null) =>
        Run(Shell(), [Slash(Path.Combine(RepoRoot(), "hooks", "pre-commit"))], env);

    [Fact]
    public void LintsExactlyTheRulesFiles_FromFileNameDecides_Blocks()
    {
        string[] paths =
        [
            "CLAUDE.md", "docs/CLAUDE.md", "AGENTS.md", "a/b/GEMINI.md", ".cursorrules", "sub/.cursorrules", ".windsurfrules",
            ".github/copilot-instructions.md", "x/.github/copilot-instructions.md", ".cursor/rules/a.mdc", "pkg/.cursor/rules/b.mdc",
            ".cursor/rules/sub/c.mdc", "notes.md", "CLAUDE.txt",
        ];
        foreach (var path in paths)
        {
            Stage(path, path.EndsWith(".mdc", StringComparison.Ordinal) ? "---\ndescription: x\nalwaysApply: true\n---\n" + CliHookTests.Contradiction : CliHookTests.Contradiction);
        }

        var (exit, stdout, stderr) = Hook();

        Assert.Equal(1, exit);
        var linted = paths.Where(p => stdout.Contains($"\n{p}: ", StringComparison.Ordinal) || stdout.StartsWith($"{p}: ", StringComparison.Ordinal)).ToList();
        Assert.Equal(paths.Where(p => RulesFormats.FromFileName(p) is not null).ToList(), linted);
        Assert.Contains("aibysitter pre-commit: commit blocked.", stderr);
    }

    [Fact]
    public void StagedContentIsLinted_NotTheWorkingCopy()
    {
        Stage("CLAUDE.md", CliHookTests.Contradiction);
        File.WriteAllText(Path.Combine(repo, "CLAUDE.md"), "# Rules\n- Use tabs.\n");

        Assert.Equal(1, Hook().Exit);
    }

    [Fact]
    public void CleanStagedFile_Passes()
    {
        Stage("CLAUDE.md", "# Rules\n- Use tabs.\n");
        Stage("notes.md", CliHookTests.Contradiction);

        Assert.Equal(0, Hook().Exit);
    }

    [Fact]
    public void HookArgs_Override()
    {
        Stage("CLAUDE.md", CliHookTests.Contradiction);

        Assert.Equal(0, Hook(new Dictionary<string, string> { ["AIBYSITTER_HOOK_ARGS"] = "--fail-on-error --disable R003" }).Exit);
        var (exit, stdout, _) = Hook(new Dictionary<string, string> { ["AIBYSITTER_HOOK_ARGS"] = "--json" });
        Assert.Equal(0, exit);
        Assert.Contains("\"file\": \"CLAUDE.md\"", stdout);
    }

    [Fact]
    public void MissingCli_WarnsAndPasses()
    {
        Stage("CLAUDE.md", CliHookTests.Contradiction);

        var (exit, _, stderr) = Hook(new Dictionary<string, string> { ["AIBYSITTER"] = "aibysitter-not-installed" });

        Assert.Equal(0, exit);
        Assert.Contains("'aibysitter-not-installed' not found; rules files not linted.", stderr);
    }
}

[Trait("Category", "Hooks")]
public class HooksPageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Downloads_ServeTheRepositoryFiles()
    {
        var client = factory.CreateClient();
        var root = Path.GetDirectoryName(typeof(HooksPageTests).Assembly.Location)!;
        while (!File.Exists(Path.Combine(root, "Aibysitter.slnx")))
        {
            root = Directory.GetParent(root)!.FullName;
        }

        foreach (var name in new[] { "pre-commit", "claude-code-settings.json" })
        {
            var response = await client.GetAsync($"/hooks/{name}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(File.ReadAllText(Path.Combine(root, "hooks", name)).Replace("\r\n", "\n"), await response.Content.ReadAsStringAsync());
        }

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/hooks/README.md")).StatusCode);
    }

    [Fact]
    public async Task Page_ShowsFiles_ExampleMatchesRealOutput_Linked()
    {
        var client = factory.CreateClient();
        var html = await client.GetStringAsync("/Hooks");

        Assert.Contains("<h1>Hooks</h1>", html);
        Assert.Contains("href=\"/hooks/pre-commit\"", html);
        Assert.Contains("aibysitter hook claude-code", html);
        Assert.Contains("is_rules_file()", html);

        var dir = Directory.CreateTempSubdirectory("aibysitter-page-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "CLAUDE.md"), CliHookTests.Contradiction);
            var (_, err) = CliHookTests.Hook(JsonSerializer.Serialize(new { tool_input = new { file_path = Path.Combine(dir, "CLAUDE.md") }, cwd = dir }));
            Assert.Contains(System.Text.Encodings.Web.HtmlEncoder.Default.Encode(err.TrimEnd()).Replace("&#xA;", "\n"), html);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }

        Assert.Contains("<a href=\"/Hooks\">Hooks</a>", await client.GetStringAsync("/"));
        Assert.Contains("href=\"/Hooks\">Hooks</a>", await client.GetStringAsync("/API"));
        Assert.Contains("https://aibysitting.net/Hooks</loc>", await client.GetStringAsync("/sitemap.xml"));
        Assert.Contains("- [Hooks](https://aibysitting.net/Hooks): ", await client.GetStringAsync("/llms.txt"));
    }
}
