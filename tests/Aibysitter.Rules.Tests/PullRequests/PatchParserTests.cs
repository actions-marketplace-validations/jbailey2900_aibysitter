using Aibysitter.Rules.PullRequests;

namespace Aibysitter.Rules.Tests.PullRequests;

public class PatchParserTests
{
    private static IReadOnlyList<DiffLine> TwoHunks() =>
        PatchParser.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "patches", "two-hunks.patch")));

    [Fact]
    public void AddedLines_CarryNewSideLineNumbers()
    {
        var added = TwoHunks().Where(l => l.Kind == DiffLineKind.Added).Select(l => (l.NewLine, l.Text));

        Assert.Equal(new (int?, string)[] { (3, "public class New"), (4, "{"), (22, "line22-added") }, added);
    }

    [Fact]
    public void RemovedLines_CarryOldSideLineNumbers()
    {
        var removed = TwoHunks().Where(l => l.Kind == DiffLineKind.Removed).Select(l => (l.OldLine, l.Text));

        Assert.Equal(new (int?, string)[] { (3, "public class Old { }"), (22, "line22-removed") }, removed);
    }

    [Fact]
    public void ContextLines_AdvanceBothSides()
    {
        var context = TwoHunks().Where(l => l.Kind == DiffLineKind.Context).Select(l => (l.OldLine, l.NewLine, l.Text));

        Assert.Equal(
            new (int?, int?, string)[] { (1, 1, "namespace Demo;"), (2, 2, ""), (4, 5, "// end"), (20, 21, "line20"), (21, 23, "line21") },
            context);
    }

    [Fact]
    public void NoNewlineMarker_IsIgnored()
    {
        Assert.DoesNotContain(TwoHunks(), l => l.Text.Contains("No newline"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NullOrEmptyPatch_ReturnsNoLines(string? patch)
    {
        Assert.Empty(PatchParser.Parse(patch));
    }

    [Fact]
    public void SingleLineHunkHeader_WithoutCounts_Parses()
    {
        var lines = PatchParser.Parse("@@ -0,0 +1 @@\n+only line");

        Assert.Equal(new DiffLine(DiffLineKind.Added, null, 1, "only line"), Assert.Single(lines));
    }

    [Fact]
    public void CrlfPatch_ParsesSameAsLf()
    {
        var lf = PatchParser.Parse("@@ -1,1 +1,2 @@\n a\n+b");
        var crlf = PatchParser.Parse("@@ -1,1 +1,2 @@\r\n a\r\n+b");

        Assert.Equal(lf, crlf);
    }

    [Fact]
    public void ChangedFile_AddedLines_UsesPatch()
    {
        var file = new ChangedFile("src/A.cs", FileChangeStatus.Modified, "@@ -1,1 +1,2 @@\n a\n+b");

        Assert.Equal(2, Assert.Single(file.AddedLines).NewLine);
    }
}
