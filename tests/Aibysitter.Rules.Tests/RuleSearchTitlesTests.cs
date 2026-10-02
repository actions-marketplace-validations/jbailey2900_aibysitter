using System.Net;
using System.Text.RegularExpressions;
using Aibysitter.Rules.PullRequests;
using Aibysitter.Web.Seo;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests;

public class RuleSearchTitlesTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly IReadOnlyList<RuleDoc> Docs = [.. RuleDocs.All, .. PullRequestCheckDocs.All];

    [Fact]
    public void EveryDoc_HasOneQuestion_AndNoExtras()
    {
        Assert.Equal(Docs.Select(d => d.Id).Order(StringComparer.Ordinal), RuleSearchTitles.ById.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Questions_EndWithQuestionMark_AreUnique()
    {
        Assert.All(RuleSearchTitles.ById.Values, q => Assert.EndsWith("?", q, StringComparison.Ordinal));
        Assert.Equal(RuleSearchTitles.ById.Count, RuleSearchTitles.ById.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task RulePages_UseQuestion_InTitleAndOgTitle_H1Unchanged()
    {
        var client = factory.CreateClient();
        foreach (var doc in Docs)
        {
            var html = await client.GetStringAsync($"/Rules/{doc.Id}");
            var expected = $"{RuleSearchTitles.ById[doc.Id]} ({doc.Id})";

            Assert.Equal($"{expected} - aibysitter", WebUtility.HtmlDecode(Regex.Match(html, "<title>(.*?)</title>").Groups[1].Value));
            Assert.Equal(expected, WebUtility.HtmlDecode(SeoHeadTests.Meta(html, "property", "og:title")));
            Assert.Contains($"<h1>{doc.Id} {doc.Name}</h1>", html);
        }
    }

    [Fact]
    public void For_FormatsQuestionWithId()
    {
        Assert.Equal("How long should CLAUDE.md be? (R004)", RuleSearchTitles.For("R004", "FileLength"));
    }
}
