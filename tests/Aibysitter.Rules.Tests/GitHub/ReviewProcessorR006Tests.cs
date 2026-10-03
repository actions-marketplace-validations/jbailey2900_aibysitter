using Aibysitter.Rules.PullRequests;
using Aibysitter.Web.GitHub;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aibysitter.Rules.Tests.GitHub;

public class ReviewProcessorR006Tests
{
    private static readonly ReviewJob Job = new(new PullRequestRef(1, "o", "r", 5, "abc123"), 777, "d1");

    private static async Task<(FakeGitHubGateway Fake, CheckRunReport Report)> Run(Action<FakeGitHubGateway> setup)
    {
        var fake = new FakeGitHubGateway();
        setup(fake);
        await new ReviewProcessor(fake, new PullRequestReviewer(), NullLogger<ReviewProcessor>.Instance).ProcessAsync(Job, CancellationToken.None);
        return (fake, await fake.Completed.Task);
    }

    [Fact]
    public async Task NoRulesFileAndNoRemovals_NoTreeFetched()
    {
        var (fake, _) = await Run(f => f.Files.Add(new ChangedFile("src/A.cs", FileChangeStatus.Modified, "@@ -1,1 +1,1 @@\n+x")));

        Assert.DoesNotContain("tree", fake.Calls);
    }

    [Fact]
    public async Task SymlinkedRulesFile_SkippedWithNote_NotFetched()
    {
        var (fake, report) = await Run(f =>
        {
            f.Files.Add(new ChangedFile("CLAUDE.md", FileChangeStatus.Added, "@@ -0,0 +1 @@\n+AGENTS.md\n\\ No newline at end of file"));
            f.Contents["CLAUDE.md"] = "# Rules\n- Handle errors properly.\n- `npm run nope`";
            f.Paths = ["AGENTS.md", "CLAUDE.md"];
            f.Symlinks.Add("CLAUDE.md");
        });

        Assert.DoesNotContain("content CLAUDE.md", fake.Calls);
        Assert.DoesNotContain(report.Annotations, a => a.Path == "CLAUDE.md");
        Assert.Contains("P014 skipped CLAUDE.md: symlink to AGENTS.md.", report.Summary);
    }

    [Fact]
    public async Task SymlinkedRulesFile_NextToRealOne_RealOneStillLinted()
    {
        var (_, report) = await Run(f =>
        {
            f.Files.Add(new ChangedFile("docs/CLAUDE.md", FileChangeStatus.Added, "@@ -0,0 +1 @@\n+../AGENTS.md"));
            f.Files.Add(new ChangedFile("AGENTS.md", FileChangeStatus.Added, "@@ -0,0 +1,2 @@\n+# Rules\n+- Handle errors properly."));
            f.Contents["AGENTS.md"] = "# Rules\n- Handle errors properly.";
            f.Contents["docs/CLAUDE.md"] = "# Rules\n- Handle errors properly.";
            f.Paths = ["AGENTS.md", "docs/CLAUDE.md"];
            f.Symlinks.Add("docs/CLAUDE.md");
        });

        Assert.Contains(report.Annotations, a => a.Path == "AGENTS.md" && a.Message.StartsWith("R002"));
        Assert.DoesNotContain(report.Annotations, a => a.Path == "docs/CLAUDE.md");
        Assert.Contains("P014 skipped docs/CLAUDE.md: symlink to ../AGENTS.md.", report.Summary);
    }

    [Fact]
    public async Task RenameOnly_UnchangedSymlinkedRulesFile_NotRead()
    {
        var (fake, _) = await Run(f =>
        {
            f.Files.Add(new ChangedFile("src/New.cs", FileChangeStatus.Renamed, PreviousPath: "src/Old.cs"));
            f.Contents["AGENTS.md"] = "- Entry point: `src/Old.cs`.";
            f.Contents["CLAUDE.md"] = "- Entry point: `src/Old.cs`.";
            f.Paths = ["AGENTS.md", "CLAUDE.md", "src/New.cs"];
            f.Symlinks.Add("CLAUDE.md");
        });

        Assert.Contains("content AGENTS.md", fake.Calls);
        Assert.DoesNotContain("content CLAUDE.md", fake.Calls);
    }

    [Fact]
    public async Task ChangedRulesFile_TreeAndNeededManifestsOnly()
    {
        var (fake, report) = await Run(f =>
        {
            f.Files.Add(new ChangedFile("CLAUDE.md", FileChangeStatus.Added, "@@ -0,0 +1,2 @@\n+- `npm run check`\n+- Use tabs."));
            f.Contents["CLAUDE.md"] = "- `npm run check`\n- Use tabs.";
            f.Contents["package.json"] = "{ \"scripts\": { \"lint\": \"x\" } }";
            f.Paths = ["CLAUDE.md", "package.json", "Makefile", "src/A.csproj"];
        });

        Assert.Contains("tree", fake.Calls);
        Assert.Contains("content package.json", fake.Calls);
        Assert.DoesNotContain("content Makefile", fake.Calls);
        Assert.DoesNotContain("content src/A.csproj", fake.Calls);
        Assert.Contains(report.Annotations, a => a.Path == "CLAUDE.md" && a.Message.StartsWith("R006 Missing identifiers: Package script `check`"));
    }

    [Fact]
    public async Task RenameOnly_ReadsUnchangedRootRulesFiles_ReportsBrokenReference()
    {
        var (fake, report) = await Run(f =>
        {
            f.Files.Add(new ChangedFile("src/New.cs", FileChangeStatus.Renamed, PreviousPath: "src/Old.cs"));
            f.Contents["AGENTS.md"] = "- Entry point: `src/Old.cs`.\n- Hooks: `src/hooks/`.";
            f.Paths = ["AGENTS.md", "src/New.cs", "docs/x.md"];
        });

        Assert.Contains("content AGENTS.md", fake.Calls);
        var annotation = Assert.Single(report.Annotations);
        Assert.Equal(("AGENTS.md", 1), (annotation.Path, annotation.Line));
    }

    [Fact]
    public async Task TruncatedTree_NoteInSummary()
    {
        var (_, report) = await Run(f =>
        {
            f.Files.Add(new ChangedFile("CLAUDE.md", FileChangeStatus.Added, "@@ -0,0 +1,1 @@\n+- `src/x/y.ts`"));
            f.Contents["CLAUDE.md"] = "- `src/x/y.ts`";
            f.Paths = null;
        });

        Assert.Contains("R006 skipped: the repository file list is too large", report.Summary);
    }

    [Fact]
    public async Task R006Disabled_TreeFetchedOnce_ForSymlinks_NoManifests()
    {
        var (fake, _) = await Run(f =>
        {
            f.Files.Add(new ChangedFile(".github/aibysitter.json", FileChangeStatus.Modified, "@@ -1,1 +1,1 @@\n+x"));
            f.Files.Add(new ChangedFile("CLAUDE.md", FileChangeStatus.Added, "@@ -0,0 +1,1 @@\n+- `npm run x`"));
            f.Contents[".github/aibysitter.json"] = "{ \"disable\": [\"R006\"] }";
            f.Contents["CLAUDE.md"] = "- `npm run x`";
            f.Paths = ["CLAUDE.md", "package.json"];
        });

        Assert.Single(fake.Calls, c => c == "tree");
        Assert.DoesNotContain("content package.json", fake.Calls);
    }

    [Fact]
    public async Task P014Disabled_NoTreeFetched()
    {
        var (fake, _) = await Run(f =>
        {
            f.Files.Add(new ChangedFile(".github/aibysitter.json", FileChangeStatus.Modified, "@@ -1,1 +1,1 @@\n+x"));
            f.Files.Add(new ChangedFile("CLAUDE.md", FileChangeStatus.Added, "@@ -0,0 +1,1 @@\n+- x"));
            f.Contents[".github/aibysitter.json"] = "{ \"disable\": [\"P014\"] }";
            f.Contents["CLAUDE.md"] = "- x";
        });

        Assert.DoesNotContain("tree", fake.Calls);
    }
}
