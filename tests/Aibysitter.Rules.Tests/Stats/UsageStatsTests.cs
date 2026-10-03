using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Aibysitter.Rules.PullRequests;
using Aibysitter.Rules.Tests.Data;
using Aibysitter.Rules.Tests.GitHub;
using Aibysitter.Web.Data;
using Aibysitter.Web.GitHub;
using Aibysitter.Web.Linting;
using Aibysitter.Web.Stats;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;
using static Aibysitter.Rules.Tests.PullRequests.PrTestData;
using static Aibysitter.Rules.Tests.RawGitHubFetcherTests;

namespace Aibysitter.Rules.Tests.Stats;

internal sealed class RecordingCounter : IUsageCounter
{
    public ConcurrentQueue<(string Metric, string Key, int By)> Events { get; } = new();

    public void Increment(string metric, string key, int by = 1) => Events.Enqueue((metric, key, by));

    public int Count(string metric, string? key = null) => Events.Where(e => e.Metric == metric && (key is null || e.Key == key)).Sum(e => e.By);
}

internal sealed class ManualTime(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

public class UsageCounterTests
{
    private static readonly DateTimeOffset Day1 = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ConcurrentIncrements_Sum_DrainResets()
    {
        var counter = new UsageCounter(new ManualTime(Day1));

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 1000; i++)
            {
                counter.Increment(UsageMetric.Lint, "form");
            }
        })));

        Assert.Equal(8000, counter.Drain()[new UsageKey(DateOnly.FromDateTime(Day1.UtcDateTime), "lint", "form")]);
        Assert.Empty(counter.Drain());
    }

    [Fact]
    public void Restore_AddsBack_OldKeysRemovedOnDrain()
    {
        var time = new ManualTime(Day1);
        var counter = new UsageCounter(time);
        counter.Increment(UsageMetric.Badge, "scored", 2);
        var taken = counter.Drain();
        counter.Restore(taken);
        counter.Increment(UsageMetric.Badge, "scored");

        Assert.Equal(3, counter.Drain().Single().Value);

        time.Now = Day1.AddDays(3);
        counter.Increment(UsageMetric.Badge, "scored");
        var drained = counter.Drain();

        Assert.Equal([DateOnly.FromDateTime(Day1.AddDays(3).UtcDateTime)], drained.Keys.Select(k => k.Date));
    }

    [Fact]
    public async Task Flush_Failure_KeepsDeltas_UntilMaxFailures()
    {
        var counter = new UsageCounter(new ManualTime(Day1));
        var flusher = new UsageStatsFlusher(counter, new ThrowingFactory(), NullLogger<UsageStatsFlusher>.Instance, new ManualTime(Day1));
        counter.Increment(UsageMetric.Lint, "api");

        for (var i = 1; i < UsageStatsFlusher.MaxFailures; i++)
        {
            Assert.Equal(0, await flusher.FlushAsync(CancellationToken.None));
        }

        Assert.Equal(1, counter.Drain().Single().Value);
        counter.Increment(UsageMetric.Lint, "api");

        for (var i = 1; i <= UsageStatsFlusher.MaxFailures; i++)
        {
            await flusher.FlushAsync(CancellationToken.None);
        }

        Assert.Empty(counter.Drain());
    }

    private sealed class ThrowingFactory : IDbContextFactory<AibysitterDbContext>
    {
        public AibysitterDbContext CreateDbContext() => throw new InvalidOperationException("no database");
    }
}

/// <summary>Against SQL Server (AIBYSITTER_TEST_SQL); skipped locally without it, required in CI.</summary>
public class UsageStatsSqlTests(ITestOutputHelper output)
{
    private static readonly DateOnly Today = new(2026, 10, 3);

    [Fact]
    public async Task Upsert_AddsToExistingRows_AndQuerySumsTotalsAndDays()
    {
        await using var database = await TestDatabase.CreateOrSkipAsync(output);
        if (database is null)
        {
            return;
        }

        await using (var db = database.Context())
        {
            await UsageStatsFlusher.UpsertAsync(db, new Dictionary<UsageKey, int>
            {
                [new(Today, "lint", "form")] = 2,
                [new(Today, "finding", "R002")] = 5,
                [new(Today.AddDays(-40), "lint", "form")] = 7,
            }, CancellationToken.None);
            await UsageStatsFlusher.UpsertAsync(db, new Dictionary<UsageKey, int> { [new(Today, "lint", "form")] = 3 }, CancellationToken.None);
        }

        await using var read = database.Context();
        Assert.Equal(5, (await read.UsageStats.SingleAsync(e => e.Date == Today && e.Metric == "lint")).Count);

        var snapshot = await EfUsageStats.QueryAsync(read, Today, CancellationToken.None);
        Assert.Equal([new UsageTotal("lint", "form", 5, 12), new UsageTotal("finding", "R002", 5, 5)], snapshot.Totals);
        Assert.Equal(30, snapshot.Days.Count);
        Assert.Equal((Today, 5L, 5L), (snapshot.Days[0].Date, snapshot.Days[0].Of("lint"), snapshot.Days[0].Of("finding")));
        Assert.Equal(0, snapshot.Days[1].Of("lint"));
    }

    [Fact]
    public async Task UnknownMetric_RejectedByConstraint()
    {
        await using var database = await TestDatabase.CreateOrSkipAsync(output);
        if (database is null)
        {
            return;
        }

        await using var db = database.Context();
        await Assert.ThrowsAnyAsync<Exception>(() => UsageStatsFlusher.UpsertAsync(db, new Dictionary<UsageKey, int> { [new(Today, "visit", "x")] = 1 }, CancellationToken.None));
    }
}

public class UsageInstrumentationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient Client(RecordingCounter usage, FakeRaw? raw = null) =>
        ScoreHistoryPageTests.Client(factory, raw ?? new FakeRaw(), new FakeScoreHistory(), usage: usage);

    [Fact]
    public async Task FormAndApiLints_CountSourceAndFindings()
    {
        var usage = new RecordingCounter();
        var client = Client(usage);

        await LintClient.PostAsync(client, "# Rules\n- Always use tabs.\n- Never use tabs.\n");
        (await client.PostAsJsonAsync(LintApi.Path, new { content = "# Rules\n- Always use tabs.\n- Never use tabs.\n" })).EnsureSuccessStatusCode();

        Assert.Equal(1, usage.Count("lint", "form"));
        Assert.Equal(1, usage.Count("lint", "api"));
        Assert.Equal(2, usage.Count("finding", "R003"));
    }

    [Fact]
    public async Task UrlLint_CountsUrl()
    {
        var usage = new RecordingCounter();
        var raw = new FakeRaw();
        raw.File("CLAUDE.md", "# Rules\n- Use tabs.\n");

        await LintClient.PostUrlAsync(Client(usage, raw), "o/r");

        Assert.Equal(1, usage.Count("lint", "url"));
    }

    [Fact]
    public async Task Badge_CountsEveryRequest_ByOutcome_NotAsLint()
    {
        var usage = new RecordingCounter();
        var raw = new FakeRaw();
        raw.File("CLAUDE.md", "# Rules\n- Always use tabs.\n- Never use tabs.\n");
        var client = Client(usage, raw);

        await client.GetAsync("/badge/o/r.svg");
        await client.GetAsync("/badge/o/r.svg");
        await client.GetAsync("/badge/o/r.svg?file=GEMINI.md");

        Assert.Equal(2, usage.Count("badge", "scored"));
        Assert.Equal(1, usage.Count("badge", "not-found"));
        Assert.Equal(0, usage.Count("lint"));
        Assert.Equal(0, usage.Count("finding"));
    }

    [Theory]
    [InlineData(null, "success", 0)]
    [InlineData("{\"conclusion\": \"fail-on-errors\"}", "failure", 1)]
    [InlineData(null, "neutral", 1)]
    public async Task Review_CountsConclusion(string? config, string key, int placeholders)
    {
        var usage = new RecordingCounter();
        var fake = new FakeGitHubGateway();
        fake.Files.Add(Added("src/A.cs", placeholders > 0 ? "var k = ::KEY::;" : "public class A { }"));
        if (config is not null)
        {
            fake.Contents[RepoConfig.FilePath] = config;
        }

        await new ReviewProcessor(fake, new PullRequestReviewer(), NullLogger<ReviewProcessor>.Instance, usage)
            .ProcessAsync(new ReviewJob(new PullRequestRef(1, "o", "r", 7, "abcdef0"), 99, "d"), CancellationToken.None);

        Assert.Equal((1, 1), (usage.Count("review"), usage.Count("review", key)));
    }

    [Fact]
    public async Task Review_Failure_CountsError()
    {
        var usage = new RecordingCounter();
        var fake = new FakeGitHubGateway { ThrowOnFiles = new HttpRequestException("down") };

        await new ReviewProcessor(fake, new PullRequestReviewer(), NullLogger<ReviewProcessor>.Instance, usage)
            .ProcessAsync(new ReviewJob(new PullRequestRef(1, "o", "r", 7, "abcdef0"), 99, "d"), CancellationToken.None);

        Assert.Equal(1, usage.Count("review", "error"));
    }

    [Fact]
    public void WithoutConnectionString_NullImplementations()
    {
        Assert.IsType<NullUsageCounter>(factory.Services.GetRequiredService<IUsageCounter>());
        Assert.IsType<NullUsageStats>(factory.Services.GetRequiredService<IUsageStats>());
    }
}

public class StatsPageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Note = "Counts are daily totals with no repository names, IP addresses or file contents. Lints run in the browser are not counted.";

    private sealed class FakeStats(UsageSnapshot? snapshot) : IUsageStats
    {
        public bool Enabled => true;

        public Task<UsageSnapshot?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }

    private Task<string> Get(IUsageStats? stats) =>
        (stats is null ? factory : factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(stats)))).CreateClient().GetStringAsync("/Stats");

    [Fact]
    public async Task Snapshot_RendersTotalsAndDays()
    {
        var today = new DateOnly(2026, 10, 3);
        var days = Enumerable.Range(0, 30).Select(i => new UsageDay(today.AddDays(-i), i == 0 ? new Dictionary<string, long> { ["lint"] = 1234, ["badge"] = 7 } : new Dictionary<string, long>())).ToList();
        var html = await Get(new FakeStats(new UsageSnapshot(
        [
            new UsageTotal("lint", "form", 1000, 1234),
            new UsageTotal("finding", "R002", 40, 90),
        ], days)));

        Assert.Contains(Note, html);
        Assert.Contains("<h2>Lints by source</h2>", html);
        Assert.Contains("<td>form</td>", html);
        Assert.Contains("<td>1,000</td>", html);
        Assert.Contains("<a href=\"/Rules/R002\">R002</a>", html);
        Assert.Contains("<h2>GitHub App reviews by conclusion</h2>", html);
        Assert.Contains("None counted yet.", html);
        Assert.Contains("<time datetime=\"2026-10-03\">3 Oct</time>", html);
        Assert.Equal(30, html.Split("<time datetime=").Length - 1);
    }

    [Fact]
    public async Task NotEnabled_AndUnavailable()
    {
        Assert.Contains("Stats are not enabled on this server.", await Get(null));
        Assert.Contains("Stats are unavailable right now.", await Get(new FakeStats(null)));
    }

    [Fact]
    public async Task Footer_Sitemap_Llms_Privacy_Serilog()
    {
        var client = factory.CreateClient();

        Assert.Contains("<a href=\"/Stats\">Stats</a>", await client.GetStringAsync("/"));
        Assert.Contains("https://aibysitting.net/Stats</loc>", await client.GetStringAsync("/sitemap.xml"));
        Assert.Contains("- [Stats](https://aibysitting.net/Stats): ", await client.GetStringAsync("/llms.txt"));
        Assert.Contains("<h2>Usage counts</h2>", await client.GetStringAsync("/Privacy"));

        var config = factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        Assert.Equal("Warning", config["Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore.Database.Command"]);
    }
}
