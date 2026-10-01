using Aibysitter.Web.Gallery;

namespace Aibysitter.Rules.Tests;

public class GalleryCatalogTests
{
    private static readonly GalleryCatalog Catalog = new(new LintEngine());

    [Fact]
    public void LoadsEightEntries_WithUniqueIds()
    {
        Assert.Equal(8, Catalog.All.Count);
        Assert.Equal(Catalog.All.Count, Catalog.All.Select(e => e.Id).Distinct().Count());
    }

    [Fact]
    public void GalleryLicenseFile_IsNotEmbedded()
    {
        var names = typeof(GalleryCatalog).Assembly.GetManifestResourceNames().Select(n => n.Replace('\\', '/'));

        Assert.DoesNotContain(names, n => n.StartsWith(GalleryCatalog.ResourcePrefix + "LICENSE", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryEntry_HasCompleteMetadata_AndCc0License()
    {
        Assert.All(Catalog.All, e =>
        {
            Assert.Contains(e.FileName, GalleryCatalog.AllowedFileNames);
            Assert.Equal("CC0-1.0", e.License);
            Assert.NotEmpty(e.Tags);
            Assert.False(string.IsNullOrWhiteSpace(e.Description));
            Assert.StartsWith("# ", e.Content);
        });
    }

    [Fact]
    public void EveryEntry_ScoresAtLeastB_AndStaysUnder200Lines()
    {
        Assert.All(Catalog.All, e =>
        {
            Assert.True(e.Score.Value >= 80, $"{e.Id} scored {e.Score.Value} ({e.Score.Grade})");
            Assert.True(e.Lines.Count <= 200, $"{e.Id} has {e.Lines.Count} lines");
        });
    }

    [Fact]
    public void CategoriesAndTags_AreDistinctAndSorted()
    {
        Assert.Equal(new[] { ".NET", "General", "Go", "JavaScript", "Python" }, Catalog.Categories);
        Assert.Equal(Catalog.Tags.Order(StringComparer.OrdinalIgnoreCase), Catalog.Tags);
        Assert.Contains("C#", Catalog.Tags);
    }

    [Theory]
    [InlineData("go-http-service")]
    [InlineData("GO-HTTP-SERVICE")]
    public void Find_IsCaseInsensitive(string id)
    {
        Assert.Equal("Go HTTP service", Catalog.Find(id)?.Name);
    }

    [Fact]
    public void Find_Unknown_ReturnsNull()
    {
        Assert.Null(Catalog.Find("nope"));
    }

    [Fact]
    public void DownloadPath_UsesIdAndFileName()
    {
        Assert.Equal("/gallery/aspnet-web-api/AGENTS.md", Catalog.Find("aspnet-web-api")!.DownloadPath);
    }
}
