using Aibysitter.Web.Content;
using Aibysitter.Web.Gallery;
using Aibysitter.Web.Notes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests;

public class MarkdownAndNotesTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public void RawHtml_IsEscaped()
    {
        var html = MarkdownRenderer.ToHtml("<script>alert(1)</script>\n\nText <b onclick=\"x()\">bold</b>");

        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<b ", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void AlignedTable_HasNoInlineStyle()
    {
        var html = MarkdownRenderer.ToHtml("| a | b |\n|:-:|--:|\n| 1 | 2 |");

        Assert.Contains("<table>", html);
        Assert.DoesNotContain("style=", html);
    }

    [Fact]
    public void Frontmatter_IsDropped_HeadingsOffset()
    {
        var html = MarkdownRenderer.ToHtml("---\nglobs: x\n---\n# Title\n## Sub", headingOffset: 2);

        Assert.DoesNotContain("globs", html);
        Assert.Contains("<h3 id=\"title\">Title</h3>", html);
        Assert.Contains("<h4 id=\"sub\">Sub</h4>", html);
    }

    [Fact]
    public void EveryGalleryEntryAndNote_RendersCspSafeHtml()
    {
        var rendered = new GalleryCatalog(new LintEngine()).All.Select(e => MarkdownRenderer.ToHtml(e.Content))
            .Concat(new NoteCatalog().All.Select(n => n.Html));

        Assert.All(rendered, html =>
        {
            Assert.DoesNotContain("style=", html);
            Assert.DoesNotContain("<script", html);
            Assert.DoesNotContain("javascript:", html);
            Assert.DoesNotMatch(@"\son\w+=", html);
        });
    }

    [Fact]
    public void FieldNote1_IsLoaded()
    {
        var note = Assert.Single(new NoteCatalog().All);

        Assert.Equal("field-note-1", note.Slug);
        Assert.Equal(new DateOnly(2026, 10, 1), note.Date);
        Assert.Contains("<table>", note.Html);
        Assert.DoesNotContain("summary:", note.Html);
    }

    [Fact]
    public async Task NotesIndex_ListsNote_AndPointsToRules()
    {
        var html = await factory.CreateClient().GetStringAsync("/Notes");

        Assert.Contains("href=\"/Notes/field-note-1\">Field note 1</a>", html);
        Assert.Contains("href=\"/Rules\">/Rules</a>", html);
    }

    [Fact]
    public async Task NotePage_RendersDataTables()
    {
        var html = await factory.CreateClient().GetStringAsync("/Notes/field-note-1");

        Assert.Contains("<h1>Field note 1</h1>", html);
        Assert.Contains("<td>R002 VagueVerbs</td>", html);
        Assert.Contains("<code>app/posts.js</code>", html);
        Assert.Contains("1 October 2026", html);
    }
}
