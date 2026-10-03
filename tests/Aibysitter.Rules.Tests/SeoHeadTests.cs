using System.Text.RegularExpressions;
using Aibysitter.Web.Gallery;
using Aibysitter.Web.Infrastructure;
using Aibysitter.Web.Seo;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aibysitter.Rules.Tests;

public partial class SeoHeadTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Base = "https://aibysitting.net";

    internal static string? Meta(string html, string attribute, string name) =>
        Regex.Match(html, $"<meta {attribute}=\"{Regex.Escape(name)}\" content=\"([^\"]*)\" />") is { Success: true } m ? m.Groups[1].Value : null;

    internal static string? Canonical(string html) => CanonicalRegex().Match(html) is { Success: true } m ? m.Groups[1].Value : null;

    [Fact]
    public async Task EverySiteMapPage_HasDescription_Canonical_AndOpenGraph()
    {
        var client = factory.CreateClient();
        var failures = new List<string>();
        foreach (var entry in factory.Services.GetRequiredService<SiteMap>().Entries)
        {
            var html = await client.GetStringAsync(entry.Path);
            var url = Base + entry.Path;
            if (string.IsNullOrWhiteSpace(Meta(html, "name", "description"))) failures.Add($"{entry.Path}: description");
            if (Meta(html, "property", "og:description") != Meta(html, "name", "description")) failures.Add($"{entry.Path}: og:description");
            if (Canonical(html) != url) failures.Add($"{entry.Path}: canonical {Canonical(html)}");
            if (Meta(html, "property", "og:url") != url) failures.Add($"{entry.Path}: og:url");
            if (string.IsNullOrWhiteSpace(Meta(html, "property", "og:title"))) failures.Add($"{entry.Path}: og:title");
            if (Meta(html, "property", "og:type") != "website" || Meta(html, "property", "og:site_name") != "aibysitter" || Meta(html, "name", "twitter:card") != "summary_large_image" || Meta(html, "property", "og:image") is not { } image || !image.EndsWith("/img/og-image.png", StringComparison.Ordinal))
            {
                failures.Add($"{entry.Path}: fixed og/twitter tags");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public async Task Descriptions_AreUniqueAcrossFixedPages()
    {
        var client = factory.CreateClient();
        var descriptions = new List<string?>();
        foreach (var path in SiteMap.FixedPages)
        {
            descriptions.Add(Meta(await client.GetStringAsync(path), "name", "description"));
        }

        Assert.Equal(descriptions.Count, descriptions.Distinct().Count());
    }

    [Theory]
    [InlineData("/Lint?sample=true", "/Lint")]
    [InlineData("/lint", "/Lint")]
    [InlineData("/api", "/API")]
    [InlineData("/Gallery/Index", "/Gallery")]
    [InlineData("/Rules/r002", "/Rules/R002")]
    [InlineData("/Rules/R002?x=1", "/Rules/R002")]
    public async Task Canonical_DropsQuery_NormalizesPath(string request, string expected)
    {
        Assert.Equal(Base + expected, Canonical(await factory.CreateClient().GetStringAsync(request)));
    }

    [Fact]
    public async Task GallerySourceView_CanonicalIsRenderedView()
    {
        var id = factory.Services.GetRequiredService<GalleryCatalog>().All[0].Id;

        Assert.Equal($"{Base}/Gallery/{id}", Canonical(await factory.CreateClient().GetStringAsync($"/Gallery/{id}/source")));
    }

    [Fact]
    public async Task BaseUrl_ComesFromConfig()
    {
        var client = factory.WithWebHostBuilder(b => b.UseSetting("Site:BaseUrl", "https://example.test")).CreateClient();

        Assert.Equal("https://example.test/About", Canonical(await client.GetStringAsync("/About")));
    }

    [Theory]
    [InlineData("http://aibysitting.net")]
    [InlineData("https://aibysitting.net/sub")]
    [InlineData("aibysitting.net")]
    public void BaseUrl_MustBeHttpsOrigin(string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([new("Site:BaseUrl", value)]).Build();

        Assert.Throws<InvalidOperationException>(() => SiteOptions.From(configuration));
    }

    [GeneratedRegex("<link rel=\"canonical\" href=\"([^\"]*)\" />")]
    private static partial Regex CanonicalRegex();
}
