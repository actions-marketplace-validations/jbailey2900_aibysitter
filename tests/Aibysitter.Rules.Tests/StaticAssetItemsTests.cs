using System.Diagnostics;
using System.Text.Json.Nodes;
using Aibysitter.Rules.Tests.Parity;

namespace Aibysitter.Rules.Tests;

/// <summary>
/// The generated wwwroot/js/generated/rules-patterns.mjs must not be a Content item at evaluation time, or a second build
/// on Windows registers it twice with static web assets (MSB4018 in DiscoverPrecompressedAssets). The exporter target adds it.
/// </summary>
public class StaticAssetItemsTests
{
    [Fact]
    public async Task GeneratedFolder_IsNotContent_AtEvaluation()
    {
        var project = Path.Combine(NodeRunner.RepoRoot, "src", "Aibysitter.Web", "Aibysitter.Web.csproj");
        Assert.True(File.Exists(Path.Combine(NodeRunner.RepoRoot, "src", "Aibysitter.Web", "wwwroot", "js", "generated", "rules-patterns.mjs")), "the test build should have generated the file");

        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in new[] { "msbuild", project, "-getItem:Content", "-p:Configuration=Release", "-nologo" })
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await process.WaitForExitAsync(timeout.Token);
        Assert.True(process.ExitCode == 0, await stderr + await stdout);

        var content = JsonNode.Parse(await stdout)!["Items"]!["Content"]!.AsArray().Select(i => i!["Identity"]!.GetValue<string>()).ToList();
        Assert.Contains(content, c => c.Replace('\\', '/').StartsWith("wwwroot/css/", StringComparison.Ordinal));
        Assert.DoesNotContain(content, c => c.Replace('\\', '/').StartsWith("wwwroot/js/generated/", StringComparison.Ordinal));
    }
}
