using Aibysitter.Rules.PullRequests;
using Aibysitter.Rules.Repo;
using Aibysitter.Rules.Rules;
using static Aibysitter.Rules.Tests.PullRequests.PrTestData;

namespace Aibysitter.Rules.Tests.Repo;

public class RepoReferencesTests
{
    private static List<(ReferenceKind, string)> Refs(string text) =>
        RepoReferences.Extract(RulesFile.Parse(text)).Select(r => (r.Kind, r.Name)).ToList();

    [Theory]
    [InlineData("- Hooks live in `src/hooks/`.", "src/hooks/")]
    [InlineData("- See [the guide](docs/guide.md#setup).", "docs/guide.md")]
    [InlineData("- Config is in src/app/config.ts today.", "src/app/config.ts")]
    [InlineData("- Run `dotnet test tests/Api.Tests/Api.Tests.csproj`.", "tests/Api.Tests/Api.Tests.csproj")]
    public void Paths(string line, string expected) => Assert.Contains((ReferenceKind.Path, expected), Refs(line));

    [Theory]
    [InlineData("- Keep `MyService.ts` next to its test.")]
    [InlineData("- Branch prefixes: `feature/`, `bugfix/`.")]
    [InlineData("- frontend/visual changes need screenshots.")]
    [InlineData("- Use `pkg/domain/agent.BuildEnv`.")]
    [InlineData("- Write `AGENTS.md/README.md`.")]
    [InlineData("- Put it in `path/to/file.ts`.")]
    [InlineData("- Add `src/<name>/index.ts`.")]
    [InlineData("- Mount `~/.config/app.json` and `/etc/hosts`.")]
    [InlineData("- Output goes to `bin/Debug/app.dll`.")]
    [InlineData("- Add a handler, e.g. `src/handlers/orders.ts`.")]
    [InlineData("- Never recreate `ui/desktop/src/api`.")]
    [InlineData("- `.claude/settings.local.json` is untracked.")]
    [InlineData("- Read `turbopack/README.md` (if exists).")]
    [InlineData("- Use `enterprise/foo/bar.clj`.")]
    [InlineData("- See https://example.com/docs/a.md.")]
    public void NotPaths(string line) => Assert.DoesNotContain(Refs(line), r => r.Item1 == ReferenceKind.Path);

    [Theory]
    [InlineData("- `npm run lint`", "lint")]
    [InlineData("- `pnpm run build:watch`", "build:watch")]
    [InlineData("- `pnpm build:watch`", "build:watch")]
    [InlineData("- `npm test`", "test")]
    [InlineData("- `yarn run e2e && yarn run lint`", "e2e")]
    public void Scripts(string line, string expected) => Assert.Contains((ReferenceKind.PackageScript, expected), Refs(line));

    [Theory]
    [InlineData("- `pnpm vitest run file.ts`")]
    [InlineData("- `npm install`")]
    [InlineData("- `pnpm add zod`")]
    [InlineData("- `bun test`")]
    [InlineData("- `bun run src/cli.ts`")]
    [InlineData("- `pnpm --filter web run build`")]
    [InlineData("- `cd web && npm run build`")]
    public void NotScripts(string line) => Assert.DoesNotContain(Refs(line), r => r.Item1 == ReferenceKind.PackageScript);

    [Fact]
    public void CodeBlock_CommandsOnly_CommentsAndPromptsStripped()
    {
        var refs = Refs("```bash\n$ make build lint   # builds ./app\nsrc/not/a/path.ts\nmake <x>\n```\n");

        Assert.Equal(new[] { (ReferenceKind.MakeTarget, "build"), (ReferenceKind.MakeTarget, "lint") }, refs);
    }

    [Fact]
    public void MsBuildTargets_BuiltinsSkipped() =>
        Assert.Equal(new[] { (ReferenceKind.MsBuildTarget, "GenerateClient") }, Refs("- `dotnet build -t:Build;GenerateClient`"));
}

public class ManifestsAndIgnoreTests
{
    [Fact]
    public void PackageScripts_ParsesJsonWithComments_NullOnInvalid()
    {
        Assert.Equal(new[] { "build", "lint" }, Manifests.PackageScripts("{ // c\n \"scripts\": { \"build\": \"tsc\", \"lint\": \"eslint .\", } }")!.Order());
        Assert.Empty(Manifests.PackageScripts("{ \"name\": \"x\" }")!);
        Assert.Null(Manifests.PackageScripts("{ nope"));
    }

    [Fact]
    public void MakeTargets_ContinuationsAndMultiTargets_NullOnIncludeOrPattern()
    {
        Assert.Equal(new[] { ".PHONY", "build", "drop-db", "reset-db", "test" },
            Manifests.MakeTargets(".PHONY: build\nbuild:\n\tgo build\ndrop-db \\\nreset-db :\n\t./do.sh\ntest: build\nX := 1\n")!.Order(StringComparer.Ordinal));
        Assert.Null(Manifests.MakeTargets("include common.mk\nbuild:\n"));
        Assert.Null(Manifests.MakeTargets("%.o: %.c\n"));
    }

    [Theory]
    [InlineData("*.log", "logs/app.log", true)]
    [InlineData("/dist", "dist/a.js", true)]
    [InlineData("/dist", "src/dist/a.js", false)]
    [InlineData("build/", "app/build/x", true)]
    [InlineData(".claude/settings.local.json", ".claude/settings.local.json", true)]
    [InlineData("docs/**/*.tmp", "docs/a/b/c.tmp", true)]
    [InlineData("# comment", "comment", false)]
    public void GitIgnore_Matches(string pattern, string path, bool ignored)
    {
        var gi = new GitIgnore();
        gi.Add(pattern, string.Empty);

        Assert.Equal(ignored, gi.IsIgnored(path));
    }

    [Fact]
    public void GitIgnore_NestedBaseDir()
    {
        var gi = new GitIgnore();
        gi.Add("*.sqlite", "database");

        Assert.True(gi.IsIgnored("database/database.sqlite"));
        Assert.False(gi.IsIgnored("other/database.sqlite"));
    }
}

public class RemovedIdentifiersTests
{
    [Fact]
    public void PathsScriptsTargets_EditsNotCounted()
    {
        var removed = RemovedIdentifiers.From(
        [
            new ChangedFile("src/Old.cs", FileChangeStatus.Removed),
            new ChangedFile("src/New.cs", FileChangeStatus.Renamed, PreviousPath: "src/Legacy.cs"),
            new ChangedFile("package.json", FileChangeStatus.Modified,
                "@@ -1,6 +1,5 @@\n {\n   \"scripts\": {\n-    \"lint\": \"eslint .\",\n-    \"build\": \"tsc\",\n+    \"build\": \"tsc -b\",\n   },\n-  \"lint\": \"x\""),
            new ChangedFile("Makefile", FileChangeStatus.Modified, "@@ -1,3 +1,1 @@\n-deploy:\n-\t./deploy.sh\n build:"),
        ]);

        Assert.Equal(new[] { "src/Legacy.cs", "src/Old.cs" }, removed.Paths.Order(StringComparer.Ordinal));
        Assert.Equal(new[] { "lint" }, removed.Scripts);
        Assert.Equal(new[] { "deploy" }, removed.MakeTargets);
        Assert.True(removed.CoversPath("src"));
        Assert.True(removed.CoversPath("src/Old.cs"));
        Assert.False(removed.CoversPath("docs"));
    }
}

public class MissingIdentifiersTests
{
    private readonly MissingIdentifiers _rule = new();

    private static RepoSnapshot Repo(Dictionary<string, string?> files) => new(files.Keys, p => files.GetValueOrDefault(p));

    private IReadOnlyList<Finding> Run(string text, Dictionary<string, string?> files, string path = "CLAUDE.md", RemovedIdentifiers? removed = null) =>
        _rule.Evaluate(RulesFile.Parse(text), path, Repo(files), removed).ToList();

    [Fact]
    public void MissingPathAndScript_Flagged_ExistingNot()
    {
        var files = new Dictionary<string, string?>
        {
            ["src/lib/hooks/useCart.ts"] = "", ["package.json"] = "{ \"scripts\": { \"lint\": \"eslint .\" } }",
        };

        var findings = Run("- Hooks: `src/hooks/`.\n- Lint: `npm run lint`.\n- Check: `npm run check-version`.\n- Lib: `src/lib/hooks/`.", files);

        Assert.Equal(new[] { 1, 3 }, findings.Select(f => f.Line));
        Assert.Equal("`src/hooks/` not found at the PR head.", findings[0].Message);
        Assert.Equal("Package script `check-version` not found in package.json.", findings[1].Message);
        Assert.All(findings, f => Assert.Equal("R006", f.RuleId));
    }

    [Theory]
    [InlineData("- See `scripts/type_check`.", "scripts/type_check.js")]
    [InlineData("- See `.github/PULL_REQUEST_TEMPLATE.md`.", ".github/pull_request_template.md")]
    [InlineData("- Import `shared/browser`.", "shared/package.json")]
    [InlineData("- Data in `database/app.sqlite`.", ".gitignore")]
    [InlineData("- Notes in `docs/old.md`.", "README.md")]
    public void Resolved_OrUnknown_NotFlagged(string text, string present)
    {
        var files = new Dictionary<string, string?> { [present] = present == ".gitignore" ? "database/*.sqlite" : "", ["database/.keep"] = "" };

        Assert.Empty(Run(text, files));
    }

    [Fact]
    public void RelativeToRulesFileDirectory()
    {
        var files = new Dictionary<string, string?> { ["packages/web/src/app.ts"] = "", ["packages/web/CLAUDE.md"] = "" };

        Assert.Empty(Run("- Entry: `src/app.ts`.", files, "packages/web/CLAUDE.md"));
        Assert.Single(Run("- Entry: `src/main.ts`.", files, "packages/web/CLAUDE.md"));
    }

    [Fact]
    public void WorkspaceRepo_ScriptsNotFoundAreUnknown()
    {
        var files = new Dictionary<string, string?> { ["package.json"] = "{ \"workspaces\": [\"packages/*\"], \"scripts\": {} }", ["packages/a/package.json"] = "{}" };

        Assert.Empty(Run("- `npm run build`", files));
    }

    [Fact]
    public void MakeTargets_ResolvedFromMakefile_UnknownWithoutOne()
    {
        Assert.Single(Run("- `make dev`", new() { ["Makefile"] = "build:\n\tgo build\n" }));
        Assert.Empty(Run("- `make dev`", new() { ["README.md"] = "" }));
    }

    [Fact]
    public void RemovedOnly_ReportsJustReferencesBrokenByThePr()
    {
        var files = new Dictionary<string, string?> { ["src/New.cs"] = "", ["package.json"] = "{ \"scripts\": {} }" };
        var removed = RemovedIdentifiers.From(
        [
            new ChangedFile("src/New.cs", FileChangeStatus.Renamed, PreviousPath: "src/Old.cs"),
            new ChangedFile("package.json", FileChangeStatus.Modified, "@@ -1,3 +1,2 @@\n   \"scripts\": {\n-    \"lint\": \"x\"\n   }"),
        ]);

        var findings = Run("- `src/Old.cs`\n- `src/Gone.cs`\n- `npm run lint`\n- `npm run other`", files, removed: removed);

        Assert.Equal(new[] { 1, 3 }, findings.Select(f => f.Line));
    }

    [Fact]
    public void ManifestsNeeded_OnlyWhatTheFileUses()
    {
        var repo = Repo(new() { ["package.json"] = null, ["Makefile"] = null, [".gitignore"] = null, ["src/A.csproj"] = null });

        Assert.Equal(new[] { ".gitignore", "package.json" }, MissingIdentifiers.ManifestsNeeded(RulesFile.Parse("- `npm run x`\n- `src/app/main.ts`"), "CLAUDE.md", repo).Order());
        Assert.Empty(MissingIdentifiers.ManifestsNeeded(RulesFile.Parse("- Use tabs."), "CLAUDE.md", repo));
    }
}

public class RulesFileLintR006Tests
{
    private readonly RulesFileLint _check = new();

    private static RepoSnapshot Repo(params string[] paths) => new(paths, _ => null);

    [Fact]
    public void ChangedRulesFile_EveryReference_EvenOnUnchangedLines()
    {
        var head = "# Rules\n- Hooks: `src/hooks/`.\n- Use tabs.";
        var file = new ChangedFile("CLAUDE.md", FileChangeStatus.Modified, "@@ -3,1 +3,1 @@\n-- Use spaces.\n+- Use tabs.", HeadContent: head);

        var finding = Assert.Single(_check.Evaluate(new PullRequestContext([file], RepoConfig.Default, Repo("src/lib/a.ts", "CLAUDE.md"))));

        Assert.Equal((2, Severity.Warning), (finding.Line, finding.Severity!.Value));
        Assert.StartsWith("R006 Missing identifiers: `src/hooks/` not found", finding.Message);
    }

    [Fact]
    public void UnchangedRulesFile_OnlyRemovals_SuppressionsHonoured()
    {
        var unchanged = new ChangedFile("AGENTS.md", FileChangeStatus.Unchanged, HeadContent: "- `src/Old.cs`\n- `src/Gone.cs`\n<!-- aibysitter-disable-next-line R006 -->\n- `src/Old.cs` again");
        var rename = new ChangedFile("src/New.cs", FileChangeStatus.Renamed, PreviousPath: "src/Old.cs");

        var findings = _check.Evaluate(new PullRequestContext([rename], RepoConfig.Default, Repo("src/New.cs", "AGENTS.md"), [unchanged])).ToList();

        Assert.Equal(("AGENTS.md", 1), (Assert.Single(findings).Path, findings[0].Line));
    }

    [Fact]
    public void NoRepo_OrR006Disabled_NoR006()
    {
        var file = Added("CLAUDE.md", "- `src/hooks/x.ts`");
        var (off, _) = RepoConfig.Parse("""{ "disable": ["R006"] }""");

        Assert.Empty(_check.Evaluate(new PullRequestContext([file], RepoConfig.Default)));
        Assert.Empty(_check.Evaluate(new PullRequestContext([file], off, Repo("src/lib/a.ts"))));
    }
}
