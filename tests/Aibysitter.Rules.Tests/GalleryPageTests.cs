using System.Net;
using System.Text.RegularExpressions;
using Aibysitter.Web.Gallery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aibysitter.Rules.Tests;

public partial class GalleryPageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static int EntryCount(string html) => EntryRegex().Matches(html).Count;

    [Fact]
    public async Task Index_ListsAllEntries_WithLinks()
    {
        var html = await factory.CreateClient().GetStringAsync("/Gallery");
        var catalog = factory.Services.GetRequiredService<GalleryCatalog>();

        Assert.Equal(13, EntryCount(html));
        Assert.All(catalog.All, e => Assert.Contains($"href=\"/Gallery/{e.Id}\"", html));
    }

    [Fact]
    public async Task CategoryFilter_ShowsOnlyThatCategory_AndMarksIt()
    {
        var html = await factory.CreateClient().GetStringAsync("/Gallery?category=Go");

        Assert.Equal(2, EntryCount(html));
        Assert.Contains("href=\"/Gallery/go-http-service\"", html);
        Assert.Contains("href=\"/Gallery/go-http-service-gemini\"", html);
        Assert.Matches("<a[^>]*aria-current=\"page\"[^>]*>Go</a>|<a[^>]*href=\"/Gallery\\?category=Go\"[^>]*aria-current=\"page\"", html);
    }

    [Fact]
    public async Task TagFilter_ShowsTaggedEntries_WithClearLink()
    {
        var html = await factory.CreateClient().GetStringAsync("/Gallery?tag=C%23");

        Assert.Equal(4, EntryCount(html));
        Assert.Contains("Tagged <strong>C#</strong>", html);
    }

    [Fact]
    public async Task UnknownFilter_ShowsEmptyState()
    {
        var html = await factory.CreateClient().GetStringAsync("/Gallery?category=Cobol");

        Assert.Equal(0, EntryCount(html));
        Assert.Contains("No examples match that filter.", html);
    }

    [Fact]
    public async Task EntryPage_ShowsNumberedFile_Score_AndDownload()
    {
        var entry = factory.Services.GetRequiredService<GalleryCatalog>().Find("go-http-service")!;

        var html = await factory.CreateClient().GetStringAsync("/Gallery/go-http-service");

        Assert.Contains("<h1>Go HTTP service</h1>", html);
        Assert.Equal(entry.Lines.Count, Regex.Matches(html, "<li id=\"L\\d+\">").Count);
        Assert.Contains("100 / 100 — A", html);
        Assert.Contains("href=\"/gallery/go-http-service/AGENTS.md\" download=\"AGENTS.md\"", html);
        Assert.Contains("href=\"/Lint?gallery=go-http-service\"", html);
        Assert.Contains("CC0 1.0", html);
    }

    [Fact]
    public async Task EntryPage_UnknownId_Returns404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync("/Gallery/nope")).StatusCode);
    }

    [Fact]
    public async Task Download_ReturnsFileAsAttachment()
    {
        var entry = factory.Services.GetRequiredService<GalleryCatalog>().Find("python-fastapi")!;

        var response = await factory.CreateClient().GetAsync("/gallery/python-fastapi/CLAUDE.md");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/markdown", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("CLAUDE.md", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal(entry.Content, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/gallery/python-fastapi/AGENTS.md")]
    [InlineData("/gallery/nope/CLAUDE.md")]
    public async Task Download_WrongFileOrId_Returns404(string path)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task LintACopy_PrefillsTextarea()
    {
        var html = await factory.CreateClient().GetStringAsync("/Lint?gallery=go-http-service");

        Assert.Contains("# Notify", html);
    }

    [Fact]
    public async Task Home_ListsGalleryEntries()
    {
        var html = await factory.CreateClient().GetStringAsync("/");

        Assert.Contains("href=\"/Gallery/minimal-starter\"", html);
        Assert.Contains("Browse the gallery", html);
    }

    [Fact]
    public async Task EntryWithFindings_ShowsNotesUnderTheirLines()
    {
        var client = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<GalleryCatalog>();
            s.AddSingleton(_ => new GalleryCatalog(new LintEngine(), typeof(GalleryPageTests).Assembly));
        })).CreateClient();

        var html = await client.GetStringAsync("/Gallery/flawed-example");

        Assert.Matches("<li id=\"L3\">.*?<p class=\"file-note sev-warning\"><a href=\"/Notes/R002\">R002</a> Vague wording", Regex.Replace(html, "\\s+", " "));
        Assert.Matches("<li id=\"L4\">.*?<p class=\"file-note sev-info\"><a href=\"/Notes/R001\">R001</a> Rationale prose", Regex.Replace(html, "\\s+", " "));
        Assert.DoesNotContain("100 / 100", html);
    }

    [GeneratedRegex("<li class=\"entry\">")]
    private static partial Regex EntryRegex();
}
