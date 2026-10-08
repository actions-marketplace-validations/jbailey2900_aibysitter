using System.Net;
using Aibysitter.Web.Data;
using Aibysitter.Web.Linting;
using Microsoft.AspNetCore.Mvc.Testing;
using static Aibysitter.Rules.Tests.RawGitHubFetcherTests;

namespace Aibysitter.Rules.Tests.Data;

public class BadgeEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Rules = "# Rules\n- Always use tabs.\n- Never use tabs.\n";

    private static async Task<(HttpResponseMessage Response, string Body)> Get(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        return (response, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Found_RendersScore_RecordsBadgeRow_CachesForAnHour()
    {
        var raw = new FakeRaw();
        raw.Routes[RawGitHubFetcher.RawUrl(new RepoRef("O", "R", false), "CLAUDE.md").ToString()] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Rules) };
        var history = new FakeScoreHistory();
        var client = ScoreHistoryPageTests.Client(factory, raw, history);

        var (response, body) = await Get(client, "/badge/O/R.svg");
        var requests = raw.Requested.Count;
        var (_, again) = await Get(client, "/badge/o/r.svg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(BadgeEndpoints.FoundCacheControl, response.Headers.CacheControl?.ToString());
        Assert.Contains(">A 90</text>", body);
        Assert.Equal(body, again);
        Assert.Equal(requests, raw.Requested.Count);
        Assert.Equal(new ScoreRecord("o/r", "CLAUDE.md", RulesetVersion.Current, 90, "A", ScoreSource.Badge), Assert.Single(history.Records));
    }

    [Fact]
    public async Task NotUtf8_IsUnknown_NoRow()
    {
        var raw = new FakeRaw();
        raw.Bytes("CLAUDE.md", Latin1);
        var history = new FakeScoreHistory();

        var (_, body) = await Get(ScoreHistoryPageTests.Client(factory, raw, history), "/badge/o/r.svg");

        Assert.Contains(">unknown</text>", body);
        Assert.Empty(history.Records);
    }

    [Fact]
    public async Task FileParameter_PicksThatFile_MissingOneIsUnknown()
    {
        var raw = new FakeRaw();
        raw.File("CLAUDE.md", "# Rules\n- Use tabs.\n");
        raw.File("AGENTS.md", Rules);
        var history = new FakeScoreHistory();
        var client = ScoreHistoryPageTests.Client(factory, raw, history);

        var (_, agents) = await Get(client, "/badge/o/r.svg?file=AGENTS.md");
        var (missing, gemini) = await Get(client, "/badge/o/r.svg?file=GEMINI.md");

        Assert.Contains(">A 90</text>", agents);
        Assert.Equal(HttpStatusCode.OK, missing.StatusCode);
        Assert.Contains(">unknown</text>", gemini);
        Assert.Equal("AGENTS.md", Assert.Single(history.Records).FileName);
    }

    [Fact]
    public async Task NotFound_UnknownGrey_NoRow_CachedForAnHour()
    {
        var raw = new FakeRaw();
        var history = new FakeScoreHistory();
        var client = ScoreHistoryPageTests.Client(factory, raw, history);

        var (response, body) = await Get(client, "/badge/o/r.svg");
        var requests = raw.Requested.Count;
        await Get(client, "/badge/o/r.svg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(">unknown</text>", body);
        Assert.Contains("fill=\"#6b6966\"", body);
        Assert.Equal(BadgeEndpoints.FoundCacheControl, response.Headers.CacheControl?.ToString());
        Assert.Equal(requests, raw.Requested.Count);
        Assert.Empty(history.Records);
    }

    [Fact]
    public async Task Unreachable_UnknownWithShortCache_NoRow()
    {
        var raw = new FakeRaw();
        foreach (var name in RawGitHubFetcher.FileNames)
        {
            raw.Routes[RawGitHubFetcher.RawUrl(RawGitHubFetcherTests.Repo, name).ToString()] = () => throw new HttpRequestException("down");
        }

        var history = new FakeScoreHistory();
        var (response, body) = await Get(ScoreHistoryPageTests.Client(factory, raw, history), "/badge/o/r.svg");

        Assert.Contains(">unknown</text>", body);
        Assert.Equal(BadgeEndpoints.TransientCacheControl, response.Headers.CacheControl?.ToString());
        Assert.Empty(history.Records);
    }

    [Theory]
    [InlineData("/badge/-bad/r.svg")]
    [InlineData("/badge/o/r.svg?file=README.md")]
    [InlineData("/badge/o/..svg")]
    public async Task InvalidInput_404Unknown_NoFetch(string path)
    {
        var raw = new FakeRaw();
        var (response, body) = await Get(ScoreHistoryPageTests.Client(factory, raw, new FakeScoreHistory()), path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(">unknown</text>", body);
        Assert.Empty(raw.Requested);
    }

    [Fact]
    public async Task HistoryOff_StillRendersFromLiveFetch()
    {
        var raw = new FakeRaw();
        raw.File("CLAUDE.md", Rules);

        var (_, body) = await Get(ScoreHistoryPageTests.Client(factory, raw, new NullScoreHistory()), "/badge/o/r.svg");

        Assert.Contains(">A 90</text>", body);
    }

    [Fact]
    public async Task RateLimited_PerIp_429()
    {
        var raw = new FakeRaw();
        var client = ScoreHistoryPageTests.Client(factory, raw, new FakeScoreHistory(), badgeLimit: 2);

        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            codes.Add((await client.GetAsync($"/badge/o/r{i}.svg")).StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], codes);
    }

    [Fact]
    public async Task BadgeLimit_DoesNotShareLintBucket()
    {
        var raw = new FakeRaw();
        var client = ScoreHistoryPageTests.Client(factory, raw, new FakeScoreHistory(), badgeLimit: 1);

        await client.GetAsync("/badge/o/r.svg");
        var lint = await LintClient.PostAsync(client, "# Rules\n- Use tabs.\n");

        Assert.Equal(HttpStatusCode.OK, lint.StatusCode);
    }
}
