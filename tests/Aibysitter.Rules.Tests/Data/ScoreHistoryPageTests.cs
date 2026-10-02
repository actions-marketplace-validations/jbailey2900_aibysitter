using System.Net;
using Aibysitter.Web.Data;
using Aibysitter.Web.Linting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static Aibysitter.Rules.Tests.RawGitHubFetcherTests;

namespace Aibysitter.Rules.Tests.Data;

public class ScoreHistoryPageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    internal static HttpClient Client(WebApplicationFactory<Program> factory, FakeRaw raw, IScoreHistory history, int badgeLimit = 100000) =>
        factory.WithWebHostBuilder(b => b
            .UseSetting("RateLimiting:Lint:PermitLimit", "100000")
            .UseSetting("RateLimiting:Badge:PermitLimit", badgeLimit.ToString())
            .ConfigureServices(s => s.AddHttpClient<RawGitHubFetcher>().ConfigurePrimaryHttpMessageHandler(() => raw))
            .ConfigureTestServices(s => s.AddSingleton(history)))
            .CreateClient();

    private async Task<string> Post(FakeRaw raw, IScoreHistory history, string repo = "o/r", string? file = null)
    {
        var response = await LintClient.PostUrlAsync(Client(factory, raw, history), repo, file);
        var html = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, LintClient.Snippet(html));
        return html;
    }

    [Fact]
    public async Task Found_RecordsOneRow_LowercasedRepo_LinkName()
    {
        var raw = new FakeRaw();
        raw.Routes[RawGitHubFetcher.RawUrl(new RepoRef("O", "R", false), "CLAUDE.md").ToString()] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("# Rules\n- Always use tabs.\n- Never use tabs.\n") };
        var history = new FakeScoreHistory();

        await Post(raw, history, repo: "https://github.com/O/R");

        var record = Assert.Single(history.Records);
        Assert.Equal(new ScoreRecord("o/r", "CLAUDE.md", RulesetVersion.Current, 90, "A", ScoreSource.LintByUrl), record);
    }

    [Fact]
    public async Task Found_ShowsLastTen_NewestFirst_WithBadgeUrl()
    {
        var raw = new FakeRaw();
        raw.File("CLAUDE.md", "# Rules\n- Use tabs.\n");
        var history = new FakeScoreHistory();
        history.Seed("o/r", "CLAUDE.md", 12, ruleset: 1);
        history.Seed("o/r", "AGENTS.md", 3);

        var html = await Post(raw, history);

        Assert.Contains("<h2 id=\"score-history-heading\">Recent scores for o/r, CLAUDE.md</h2>", html);
        Assert.Equal(10, html.Split("<time datetime=").Length - 1);
        Assert.Contains("<td>100</td>", html);
        Assert.Contains($"<td>v{RulesetVersion.Current}</td>", html);
        Assert.Contains("<td>v1 (older)</td>", html);
        Assert.Contains("<td>81</td>", html);
        Assert.DoesNotContain("<td>72</td>", html);
        Assert.Contains("Badge for a README: <code>https://aibysitting.net/badge/o/r.svg</code>", html);
    }

    [Fact]
    public async Task NonDefaultFile_BadgeUrlNamesFile()
    {
        var raw = new FakeRaw();
        raw.File("CLAUDE.md", "# Rules\n- Use tabs.\n");
        raw.File(".github/copilot-instructions.md", "# Rules\n- Use spaces.\n");

        var html = await Post(raw, new FakeScoreHistory(), file: ".github/copilot-instructions.md");

        Assert.Contains("Recent scores for o/r, .github/copilot-instructions.md", html);
        Assert.Contains("https://aibysitting.net/badge/o/r.svg?file=.github%2Fcopilot-instructions.md", html);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("timeout")]
    [InlineData("too-large")]
    public async Task NotFound_OrFailed_RecordsNothing_NoHistory(string? mode)
    {
        var raw = new FakeRaw();
        if (mode == "too-large")
        {
            raw.File("CLAUDE.md", new string('a', RawGitHubFetcher.MaxBytes + 1));
        }
        else if (mode == "timeout")
        {
            raw.Routes[RawGitHubFetcher.RawUrl(RawGitHubFetcherTests.Repo, "CLAUDE.md").ToString()] = () => throw new HttpRequestException("down");
        }

        var history = new FakeScoreHistory();
        var html = await Post(raw, history);

        Assert.Empty(history.Records);
        Assert.DoesNotContain("score-history", html);
    }

    [Fact]
    public async Task PastedText_IsNeverRecorded()
    {
        var history = new FakeScoreHistory();
        var client = Client(factory, new FakeRaw(), history);

        var response = await LintClient.PostAsync(client, "# Rules\n- Use tabs.\n");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(history.Records);
    }

    [Fact]
    public async Task HistoryOff_NoSection()
    {
        var raw = new FakeRaw();
        raw.File("CLAUDE.md", "# Rules\n- Use tabs.\n");

        var html = await Post(raw, new NullScoreHistory());

        Assert.DoesNotContain("score-history", html);
        Assert.Contains("<h2>Findings (0)</h2>", html);
    }
}
