using System.Text.RegularExpressions;
using Aibysitter.Rules.Tests.Parity;

namespace Aibysitter.Rules.Tests;

public class GoingPublicChecklistTests
{
    private static string Checklist => File.ReadAllText(Path.Combine(NodeRunner.RepoRoot, "docs", "going-public.md"));

    /// <summary>Rows of the "text that changes" table: file and current text.</summary>
    private static List<(string File, string Text)> Rows() =>
        Regex.Matches(Checklist, @"^\| `(?<file>[^`]+)` \| (?<text>[^|]+?) \|", RegexOptions.Multiline)
            .Select(m => (m.Groups["file"].Value, m.Groups["text"].Value))
            .ToList();

    [Fact]
    public void TextThatChanges_IsStillInEachFile()
    {
        var rows = Rows();

        Assert.Equal(5, rows.Count);
        Assert.All(rows, r => Assert.Contains(r.Text, File.ReadAllText(Path.Combine(NodeRunner.RepoRoot, r.File)), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("refs/pull/1–14/head")]
    [InlineData("Private vulnerability reporting")]
    [InlineData("`build-test` and `catalogs-windows`")]
    [InlineData("`aibysitter-github-actions`")]
    [InlineData("`false positive`")]
    [InlineData("docs/brand/social-preview-1280x640.png")]
    [InlineData("`release-cli.yml`")]
    [InlineData("tag `v1.0.0` and `v1`")]
    public void Checklist_Covers(string text) => Assert.Contains(text, Checklist, StringComparison.Ordinal);
}
