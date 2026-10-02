using System.Collections.Concurrent;
using System.Net;
using Aibysitter.Web.Linting;

namespace Aibysitter.Rules.Tests;

public class RawGitHubFetcherTests
{
    private static readonly RepoRef Repo = new("o", "r", false);

    /// <summary>Answers by full URL; unknown URLs get 404. Records every request.</summary>
    internal sealed class FakeRaw : HttpMessageHandler
    {
        public ConcurrentDictionary<string, Func<HttpResponseMessage>> Routes { get; } = new();

        public ConcurrentQueue<string> Requested { get; } = new();

        public TimeSpan Delay { get; set; }

        public void File(string name, string content) =>
            Routes[RawGitHubFetcher.RawUrl(Repo, name).ToString()] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requested.Enqueue(request.RequestUri!.ToString());
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            return Routes.TryGetValue(request.RequestUri!.ToString(), out var route) ? route() : new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private static RawGitHubFetcher Fetcher(FakeRaw raw, TimeSpan? timeout = null) =>
        new(new HttpClient(raw)) { Timeout = timeout ?? TimeSpan.FromSeconds(5) };

    [Fact]
    public async Task ProbesExactlyTheSixRawUrls()
    {
        var raw = new FakeRaw();

        await Fetcher(raw).FetchAsync(Repo, null, CancellationToken.None);

        Assert.Equal(
            RawGitHubFetcher.FileNames.Select(n => $"https://raw.githubusercontent.com/o/r/HEAD/{n}").Order(),
            raw.Requested.Order());
    }

    [Fact]
    public async Task FirstInFixedOrderWins_OthersListed()
    {
        var raw = new FakeRaw();
        raw.File(".cursorrules", "- c");
        raw.File("AGENTS.md", "- a");
        raw.File("GEMINI.md", "- g");

        var result = await Fetcher(raw).FetchAsync(Repo, null, CancellationToken.None);

        Assert.Equal(FetchStatus.Found, result.Status);
        Assert.Equal("AGENTS.md", result.FileName);
        Assert.Equal("- a", result.Content);
        Assert.Equal(new[] { "GEMINI.md", ".cursorrules" }, result.OtherFiles);
    }

    [Fact]
    public async Task PreferredFileWins_WhenPresent()
    {
        var raw = new FakeRaw();
        raw.File("CLAUDE.md", "- c");
        raw.File(".windsurfrules", "- w");

        var result = await Fetcher(raw).FetchAsync(Repo, ".windsurfrules", CancellationToken.None);

        Assert.Equal(".windsurfrules", result.FileName);
        Assert.Equal(new[] { "CLAUDE.md" }, result.OtherFiles);
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("README.md")]
    public async Task UnlistedPreferredName_IsIgnored(string preferred)
    {
        var raw = new FakeRaw();
        raw.File("CLAUDE.md", "- c");

        var result = await Fetcher(raw).FetchAsync(Repo, preferred, CancellationToken.None);

        Assert.Equal("CLAUDE.md", result.FileName);
        Assert.DoesNotContain(raw.Requested, u => u.Contains(preferred, StringComparison.Ordinal));
    }

    [Fact]
    public async Task NothingFound_IsNotFound()
    {
        var result = await Fetcher(new FakeRaw()).FetchAsync(Repo, null, CancellationToken.None);

        Assert.Equal(FetchStatus.NotFound, result.Status);
        Assert.Null(result.FileName);
    }

    [Fact]
    public async Task RedirectWithinRaw_IsFollowed()
    {
        var raw = new FakeRaw();
        raw.Routes[RawGitHubFetcher.RawUrl(Repo, "CLAUDE.md").ToString()] = () => Redirect("https://raw.githubusercontent.com/o/renamed/HEAD/CLAUDE.md");
        raw.Routes["https://raw.githubusercontent.com/o/renamed/HEAD/CLAUDE.md"] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("- moved") };

        var result = await Fetcher(raw).FetchAsync(Repo, null, CancellationToken.None);

        Assert.Equal("- moved", result.Content);
    }

    [Theory]
    [InlineData("https://evil.example/x")]
    [InlineData("http://raw.githubusercontent.com/o/r/HEAD/CLAUDE.md")]
    [InlineData("https://raw.githubusercontent.com:8443/o/r/HEAD/CLAUDE.md")]
    [InlineData("https://user@raw.githubusercontent.com/o/r/HEAD/CLAUDE.md")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    public async Task RedirectOffRaw_IsRefused(string target)
    {
        var raw = new FakeRaw();
        raw.Routes[RawGitHubFetcher.RawUrl(Repo, "CLAUDE.md").ToString()] = () => Redirect(target);

        var result = await Fetcher(raw).FetchAsync(Repo, null, CancellationToken.None);

        Assert.Equal(FetchStatus.NotFound, result.Status);
        Assert.DoesNotContain(raw.Requested, u => u == new Uri(target).ToString());
    }

    [Fact]
    public async Task MoreThanThreeRedirects_IsRefused()
    {
        var raw = new FakeRaw();
        var hops = Enumerable.Range(0, 5).Select(i => $"https://raw.githubusercontent.com/o/r{i}/HEAD/CLAUDE.md").ToList();
        raw.Routes[RawGitHubFetcher.RawUrl(Repo, "CLAUDE.md").ToString()] = () => Redirect(hops[0]);
        for (var i = 0; i < hops.Count - 1; i++)
        {
            var next = hops[i + 1];
            raw.Routes[hops[i]] = () => Redirect(next);
        }

        raw.Routes[hops[^1]] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("- too far") };

        var result = await Fetcher(raw).FetchAsync(Repo, null, CancellationToken.None);

        Assert.Equal(FetchStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task OverByteCap_IsTooLarge()
    {
        var raw = new FakeRaw();
        raw.File("CLAUDE.md", new string('x', RawGitHubFetcher.MaxBytes + 1));

        var result = await Fetcher(raw).FetchAsync(Repo, null, CancellationToken.None);

        Assert.Equal(FetchStatus.TooLarge, result.Status);
        Assert.Equal("CLAUDE.md", result.FileName);
        Assert.Null(result.Content);
    }

    [Fact]
    public async Task AtCharacterLimit_IsFound_OverIt_TooLarge()
    {
        var raw = new FakeRaw();
        raw.File("CLAUDE.md", new string('x', LintLimits.MaxContentLength));
        raw.File("AGENTS.md", new string('x', LintLimits.MaxContentLength + 1));

        Assert.Equal(FetchStatus.Found, (await Fetcher(raw).FetchAsync(Repo, null, CancellationToken.None)).Status);
        Assert.Equal(FetchStatus.TooLarge, (await Fetcher(raw).FetchAsync(Repo, "AGENTS.md", CancellationToken.None)).Status);
    }

    [Fact]
    public async Task SlowResponses_TimeOut()
    {
        var raw = new FakeRaw { Delay = TimeSpan.FromSeconds(5) };
        raw.File("CLAUDE.md", "- c");

        var result = await Fetcher(raw, TimeSpan.FromMilliseconds(100)).FetchAsync(Repo, null, CancellationToken.None);

        Assert.Equal(FetchStatus.TimedOut, result.Status);
    }

    [Fact]
    public async Task ServerErrors_AreUnreachable()
    {
        var raw = new FakeRaw();
        foreach (var name in RawGitHubFetcher.FileNames)
        {
            raw.Routes[RawGitHubFetcher.RawUrl(Repo, name).ToString()] = () => new HttpResponseMessage(HttpStatusCode.BadGateway);
        }

        Assert.Equal(FetchStatus.Unreachable, (await Fetcher(raw).FetchAsync(Repo, null, CancellationToken.None)).Status);
    }

    private static HttpResponseMessage Redirect(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
        response.Headers.Location = new Uri(location);
        return response;
    }
}
