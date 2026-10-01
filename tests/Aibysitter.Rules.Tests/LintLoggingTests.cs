using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace Aibysitter.Rules.Tests;

[CollectionDefinition(nameof(LintLoggingTests), DisableParallelization = true)]
public sealed class LintLoggingCollection;

/// <summary>
/// Lint form text never reaches a log event, at any level, on success or failure paths.
/// Runs alone: UseSerilog replaces the static logger per host, so parallel hosts would capture each other's events.
/// </summary>
[Collection(nameof(LintLoggingTests))]
public class LintLoggingTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Marker = "ZqMarker7731";

    private sealed class CapturingSink : ILogEventSink
    {
        public ConcurrentQueue<string> Events { get; } = new();

        public void Emit(LogEvent logEvent) =>
            Events.Enqueue($"{logEvent.RenderMessage()} {string.Join(" ", logEvent.Properties.Select(p => $"{p.Key}={p.Value}"))} {logEvent.Exception}");
    }

    private (HttpClient Client, CapturingSink Sink) Create()
    {
        var sink = new CapturingSink();
        var client = factory.WithWebHostBuilder(b => b
            .UseSetting("Serilog:MinimumLevel:Default", "Verbose")
            .UseSetting("Serilog:MinimumLevel:Override:Microsoft.AspNetCore", "Verbose")
            .ConfigureServices(s => s.AddSingleton<ILogEventSink>(sink)))
            .CreateClient();
        return (client, sink);
    }

    private static void AssertNoMarker(CapturingSink sink)
    {
        Assert.NotEmpty(sink.Events);
        Assert.DoesNotContain(sink.Events, e => e.Contains(Marker, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidPost_TextNotLogged()
    {
        var (client, sink) = Create();

        var response = await LintClient.PostAsync(client, $"- Use Password={Marker}; to connect.\n");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains(sink.Events, e => e.Contains("Linted", StringComparison.Ordinal));
        AssertNoMarker(sink);
    }

    [Fact]
    public async Task OversizedPost_TextNotLogged()
    {
        var (client, sink) = Create();

        await LintClient.PostAsync(client, Marker + new string('x', 100_001));

        AssertNoMarker(sink);
    }

    [Fact]
    public async Task PostWithoutAntiforgeryToken_TextNotLogged()
    {
        var (client, sink) = Create();
        using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["RulesText"] = Marker });

        var response = await client.PostAsync("/Lint", content);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        AssertNoMarker(sink);
    }
}
