using Aibysitter.Rules.PullRequests;
using static Aibysitter.Rules.Tests.PullRequests.PrTestData;

namespace Aibysitter.Rules.Tests.PullRequests;

/// <summary>Corpus PR pass 1: P003 and P012 withdrawn, P001 TODO_ dropped, P007 string literals, P008 whole packages.</summary>
public class CorpusPr1TuningTests
{
    private static ChangedFile Removed(string path) => new(path, FileChangeStatus.Removed, "@@ -1,1 +0,0 @@\n-x");

    // Withdrawals

    [Theory]
    [InlineData("P003 AssertNothingTests withdrawn")]
    [InlineData("P012 DebugLeftovers withdrawn")]
    public void Withdrawal_InChangelog(string title) =>
        Assert.Contains(RulesetChangelog.AppChecks, e => e.Title == title && e.Changes.Single().EndsWith("The ID stays reserved.", StringComparison.Ordinal));

    [Theory]
    [InlineData("P003")]
    [InlineData("P012")]
    public void WithdrawnId_InIgnoreChecks_IsUnknown(string id)
    {
        var (_, errors) = RepoConfig.Parse($$"""{ "ignore": [{ "paths": ["docs/**"], "checks": ["{{id}}"] }] }""");

        Assert.Contains(errors, e => e.Message.Contains("is not a known check ID", StringComparison.Ordinal));
    }

    [Fact]
    public void DebugStatements_NoLongerFlagged() =>
        Assert.Empty(new PullRequestReviewer().Review(Context(Added("src/run.ts", "console.log(`Executing: ${cmd}`);", "debugger;"))).Findings);

    // P001

    [Fact]
    public void P001_OtherPatterns_StillFlagged() =>
        Assert.Equal(3, new PlaceholderIdentifiers().Evaluate(Context(Added("src/appsettings.json", "\"a\": \"::ZONE_ID::\",", "\"b\": \"REPLACE_ME\",", "\"c\": \"YOUR_KEY\""))).Count());

    // P007

    [Theory]
    [InlineData("src/A.cs", "var s = \"#pragma warning disable CS1591\";")]
    [InlineData("src/a.ts", "const banner = '/* eslint-disable */';")]
    [InlineData("src/a.py", "LINE = \"x = 1  # noqa\"")]
    public void P007_InsideStringOnLine_NotFlagged(string path, string line) =>
        Assert.Empty(new SuppressedDiagnostics().Evaluate(Context(Added(path, line))));

    [Theory]
    [InlineData("tests/AppHostTests.cs", "await File.WriteAllTextAsync(path, $$\"\"\"", "    #pragma warning disable ASPIREPIPELINES001", "    var b = 1;", "    \"\"\");")]
    [InlineData("src/gen.ts", "const header = `", "/* eslint-disable */", "`;")]
    [InlineData("tools/gen.py", "TEMPLATE = \"\"\"", "import os  # noqa", "\"\"\"")]
    [InlineData("src/A.cs", "var s = @\"first", "#pragma warning disable CS1", "last\";")]
    public void P007_InsideMultiLineString_NotFlagged(string path, params string[] lines) =>
        Assert.Empty(new SuppressedDiagnostics().Evaluate(Context(Added(path, lines))));

    [Theory]
    [InlineData("src/A.cs", "var s = \"\"\"", "text", "\"\"\";", "#pragma warning disable CS8602")]
    [InlineData("src/a.ts", "const t = `x`;", "// eslint-disable-next-line no-console")]
    [InlineData("src/A.cs", "var s = \"a\"; #pragma warning disable CS1")]
    public void P007_AfterStringCloses_StillFlagged(string path, params string[] lines) =>
        Assert.Single(new SuppressedDiagnostics().Evaluate(Context(Added(path, lines))));

    [Fact]
    public void StringLines_ResetAtDiffGap()
    {
        var file = new ChangedFile("src/A.cs", FileChangeStatus.Modified, "@@ -1,1 +1,1 @@\n+var s = \"\"\"\n@@ -10,1 +10,1 @@\n+#pragma warning disable CS1");

        Assert.Empty(CodeText.StringLines(file));
        Assert.Single(new SuppressedDiagnostics().Evaluate(Context(file)));
    }

    [Fact]
    public void StringLines_FromHeadContent()
    {
        var file = Added("src/a.ts", "const a = `one", "two", "three`;", "const b = 1;");

        Assert.Equal([2, 3], CodeText.StringLines(file).Order());
    }

    // P008

    [Fact]
    public void P008_PackageFolderGone_NotFlagged()
    {
        var context = new PullRequestContext(
            [Removed("sdk/astro/arm-astro/test/ops.spec.ts"), Removed("sdk/astro/arm-astro/src/index.ts")],
            RepoConfig.Default,
            HeadFiles: ["sdk/other/pkg/src/index.ts", "README.md"]);

        Assert.Empty(new DeletedTests().Evaluate(context));
    }

    [Fact]
    public void P008_PackageFolderKept_Flagged()
    {
        var context = new PullRequestContext(
            [Removed("base-action/test/run-claude.test.ts"), Removed("base-action/scripts/pre-push.sh")],
            RepoConfig.Default,
            HeadFiles: ["base-action/src/run-claude.ts", "src/index.ts"]);

        Assert.Single(new DeletedTests().Evaluate(context));
    }

    [Fact]
    public void P008_TestProjectForRemovedSourceProject_NotFlagged()
    {
        var context = new PullRequestContext(
            [
                Removed("dotnet/test/VectorData/AzureAISearch.ConformanceTests/FilterTests.cs"),
                Removed("dotnet/test/VectorData/AzureAISearch.UnitTests/MapperTests.cs"),
                Removed("dotnet/src/VectorData/AzureAISearch/AzureAISearchMapper.cs"),
            ],
            RepoConfig.Default,
            HeadFiles: ["dotnet/src/VectorData/AzureAISearch/README.md", "dotnet/src/VectorData/Qdrant/QdrantMapper.cs", "dotnet/test/VectorData/Qdrant.UnitTests/MapperTests.cs"]);

        Assert.Empty(new DeletedTests().Evaluate(context));
    }

    [Fact]
    public void P008_SourceProjectFolderStillExists_Flagged()
    {
        var context = new PullRequestContext(
            [Removed("dotnet/test/VectorData/AzureAISearch.ConformanceTests/FilterTests.cs"), Removed("dotnet/src/VectorData/AzureAISearch/Old.cs")],
            RepoConfig.Default,
            HeadFiles: ["dotnet/src/VectorData/AzureAISearch/AzureAISearchMapper.cs"]);

        Assert.Single(new DeletedTests().Evaluate(context));
    }

    [Fact]
    public void P008_OnlyTestProjectRemoved_Flagged()
    {
        var context = new PullRequestContext(
            [Removed("test/Orders.Tests/OrderTests.cs"), Removed("test/Orders.Tests/Helpers.cs")],
            RepoConfig.Default,
            HeadFiles: ["src/Orders/Order.cs"]);

        Assert.Equal(2, new DeletedTests().Evaluate(context).Count());
    }

    [Fact]
    public void P008_NoHeadFileList_NoPackageExemption() =>
        Assert.Single(new DeletedTests().Evaluate(Context(Removed("sdk/pkg/test/a.spec.ts"), Removed("sdk/pkg/src/b.ts"))));
}
