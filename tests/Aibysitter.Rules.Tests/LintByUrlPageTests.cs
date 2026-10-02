using System.Net;
using Aibysitter.Web.Linting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using static Aibysitter.Rules.Tests.RawGitHubFetcherTests;

namespace Aibysitter.Rules.Tests;

public class LintByUrlPageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    internal static HttpClient ClientWith(WebApplicationFactory<Program> factory, FakeRaw raw, int permitLimit = 100000) =>
        factory.WithWebHostBuilder(b => b
            .UseSetting("RateLimiting:Lint:PermitLimit", permitLimit.ToString())
            .ConfigureServices(s => s.AddHttpClient<RawGitHubFetcher>().ConfigurePrimaryHttpMessageHandler(() => raw)))
            .CreateClient();

    private async Task<(string Html, FakeRaw Raw)> Post(Action<FakeRaw> setup, string repo = "o/r", string? file = null)
    {
        var raw = new FakeRaw();
        setup(raw);
        var response = await LintClient.PostUrlAsync(ClientWith(factory, raw), repo, file);
        var html = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, LintClient.Snippet(html));
        return (html, raw);
    }

    [Fact]
    public async Task Form_IsOnLintPage()
    {
        var html = await factory.CreateClient().GetStringAsync("/Lint");

        Assert.Contains("<form method=\"post\" id=\"url-form\" class=\"url-form\" action=\"/Lint?handler=Url\">", html);
        Assert.Contains("Nothing is stored or logged.", html);
    }

    [Fact]
    public async Task Found_LintsFirstFile_LinksRaw_OffersOthers_PrefillsForm()
    {
        var (html, _) = await Post(raw =>
        {
            raw.File("CLAUDE.md", "# Rules\n- Always use tabs.\n- Never use tabs.");
            raw.File(".cursorrules", "# Rules\n- Use tabs.");
        });

        Assert.Contains("Linted <code>CLAUDE.md</code> from o/r, default branch. <a href=\"https://raw.githubusercontent.com/o/r/HEAD/CLAUDE.md\">Raw file</a>", html);
        Assert.Contains("<h2>Findings (1)</h2>", html);
        Assert.Contains("<a href=\"/Rules/R003\">R003</a>", html);
        Assert.Contains("<input type=\"hidden\" name=\"file\" value=\".cursorrules\" />", html);
        Assert.Contains("Lint .cursorrules instead", html);
        Assert.Contains("- Always use tabs.", html);
        Assert.Contains("<option selected=\"selected\" value=\"ClaudeMd\">", html);
        Assert.Contains("value=\"o/r\"", html);
    }

    [Fact]
    public async Task ChosenFile_IsLinted_WithItsFormat()
    {
        var (html, _) = await Post(raw =>
        {
            raw.File("CLAUDE.md", "# Rules\n- a");
            raw.File(".cursorrules", "# Rules\n- Use tabs.");
        }, file: ".cursorrules");

        Assert.Contains("Linted <code>.cursorrules</code> from o/r", html);
        Assert.Contains("<option selected=\"selected\" value=\"CursorRules\">", html);
        Assert.Contains("Lint CLAUDE.md instead", html);
    }

    [Fact]
    public async Task ExtraPath_IsNoted()
    {
        var (html, _) = await Post(raw => raw.File("CLAUDE.md", "# Rules\n- a"), repo: "https://github.com/o/r/tree/dev");

        Assert.Contains("The URL named a branch or path; the default branch was linted.", html);
    }

    [Fact]
    public async Task InvalidInput_PlainMessage_NoFetch()
    {
        var (html, raw) = await Post(_ => { }, repo: "https://evil.example/o/r");

        Assert.Contains(UrlLintResult.InvalidInput, html);
        Assert.Empty(raw.Requested);
        Assert.DoesNotContain("field-validation-error", html);
    }

    [Theory]
    [InlineData("missing", "No supported rules file found on the default branch of o/r. Private repositories can&#x27;t be read.")]
    [InlineData("large", "CLAUDE.md is over 100 KB.")]
    [InlineData("down", "GitHub could not be reached. Try again later.")]
    public async Task Failures_PlainMessage_NoResults(string kind, string message)
    {
        var (html, _) = await Post(raw =>
        {
            if (kind == "large")
            {
                raw.File("CLAUDE.md", new string('x', RawGitHubFetcher.MaxBytes + 1));
            }
            else if (kind == "down")
            {
                foreach (var name in RawGitHubFetcher.FileNames)
                {
                    raw.Routes[RawGitHubFetcher.RawUrl(new RepoRef("o", "r", false), name).ToString()] = () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                }
            }
        });

        Assert.Contains($"<p class=\"error\" role=\"alert\">{message}</p>", html);
        Assert.DoesNotContain("<h2>Findings", html);
    }

    [Fact]
    public async Task Symlink_LintsTarget_NamesLink_KeepsLinkFormat()
    {
        var (html, _) = await Post(raw =>
        {
            raw.File("CLAUDE.md", ".ai/AGENTS.md");
            raw.Routes["https://raw.githubusercontent.com/o/r/HEAD/.ai/AGENTS.md"] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("# Rules\n- Always use tabs.\n- Never use tabs.") };
        });

        Assert.Contains("Linted <code>.ai/AGENTS.md</code> from o/r, default branch: <code>CLAUDE.md</code> links to it. <a href=\"https://raw.githubusercontent.com/o/r/HEAD/.ai/AGENTS.md\">Raw file</a>", html);
        Assert.Contains("<h2>Findings (1)</h2>", html);
        Assert.Contains("<option selected=\"selected\" value=\"ClaudeMd\">", html);
    }

    [Fact]
    public async Task LinkToLink_ShowsMessage()
    {
        var (html, _) = await Post(raw =>
        {
            raw.File("CLAUDE.md", ".ai/AGENTS.md");
            raw.Routes["https://raw.githubusercontent.com/o/r/HEAD/.ai/AGENTS.md"] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("../shared/AGENTS.md") };
        });

        Assert.Contains("<p class=\"error\" role=\"alert\">CLAUDE.md links to .ai/AGENTS.md, which looks like a link to shared/AGENTS.md. Links are followed one level.</p>", html);
        Assert.DoesNotContain("<h2>Findings", html);
    }
}
