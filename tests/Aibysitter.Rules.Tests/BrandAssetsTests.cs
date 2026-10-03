using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests;

public class BrandAssetsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/favicon.ico", "image/x-icon")]
    [InlineData("/favicon.svg", "image/svg+xml")]
    [InlineData("/apple-touch-icon.png", "image/png")]
    [InlineData("/img/icon-192.png", "image/png")]
    [InlineData("/img/icon-512.png", "image/png")]
    [InlineData("/img/og-image.png", "image/png")]
    [InlineData("/img/mark.svg", "image/svg+xml")]
    public async Task Asset_ServedWithContentType(string path, string contentType)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.True(response.IsSuccessStatusCode, $"{path} returned {(int)response.StatusCode}");
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Manifest_Parses_AndEveryIconResolves()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/site.webmanifest");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("application/manifest+json", response.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("aibysitter", doc.RootElement.GetProperty("name").GetString());
        foreach (var icon in doc.RootElement.GetProperty("icons").EnumerateArray())
        {
            Assert.True((await client.GetAsync(icon.GetProperty("src").GetString())).IsSuccessStatusCode);
        }
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Rules/R001")]
    [InlineData("/Notes/field-note-3")]
    public async Task Layout_LinksIcons_AndOgImage(string path)
    {
        var html = await factory.CreateClient().GetStringAsync(path);

        Assert.Contains("<link rel=\"icon\" href=\"/favicon.ico\" sizes=\"48x48\" />", html);
        Assert.Contains("<link rel=\"icon\" href=\"/favicon.svg\" type=\"image/svg+xml\" />", html);
        Assert.Contains("<link rel=\"apple-touch-icon\" href=\"/apple-touch-icon.png\" />", html);
        Assert.Contains("<link rel=\"manifest\" href=\"/site.webmanifest\" />", html);
        Assert.Matches("<meta property=\"og:image\" content=\"https?://[^\"]+/img/og-image.png\" />", html);
        Assert.Contains("<meta property=\"og:image:width\" content=\"1200\" />", html);
        Assert.Contains("<meta name=\"twitter:card\" content=\"summary_large_image\" />", html);
    }

    [Fact]
    public async Task Header_HasDecorativeMark_BeforeWordmark()
    {
        var html = await factory.CreateClient().GetStringAsync("/");

        Assert.Matches("<a class=\"wordmark\"[^>]*aria-label=\"aibysitter home\"[^>]*><svg class=\"mark\" viewBox=\"0 0 512 512\" aria-hidden=\"true\" focusable=\"false\">", html);
        Assert.Contains("</svg>aibysitter<span class=\"cursor\" aria-hidden=\"true\"></span></a>", html);
    }
}
