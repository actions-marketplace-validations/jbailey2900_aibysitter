using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Aibysitter.Cli;
using Aibysitter.Rules.Tests.Parity;
using Aibysitter.Web.Linting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests.Cli;

public class CliParityTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task JsonOutput_EqualsApiResponse_OnEveryParityInput()
    {
        var client = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:Lint:PermitLimit", "100000")).CreateClient();
        var inputs = ParityInputs.All
            .Where(i => !string.IsNullOrWhiteSpace(i.Text) && i.Text.Length <= LintLimits.MaxContentLength)
            .ToList();
        var mismatches = new List<string>();

        foreach (var input in inputs)
        {
            var disable = input.Disable ?? [];
            var api = JsonNode.Parse(await (await client.PostAsJsonAsync(LintApi.Path, new { content = input.Text, format = input.Format.ToString(), disable }))
                .Content.ReadAsStringAsync())!;

            string[] args = ["lint", "-", "--json", "--format", input.Format.ToString(), .. disable.Count > 0 ? new[] { "--disable", string.Join(',', disable) } : []];
            var result = CliTests.Run(args, input.Text);
            var cli = JsonNode.Parse(result.Out)!.AsObject();
            cli.Remove("file");

            if (result.Exit != CliApp.Ok || !JsonNode.DeepEquals(api, cli))
            {
                mismatches.Add($"{input.Name}\n  API: {api.ToJsonString()}\n  CLI: {cli.ToJsonString()}");
            }
        }

        Assert.True(inputs.Count > 50, $"only {inputs.Count} inputs");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} of {inputs.Count} differ:\n" + string.Join("\n", mismatches.Take(5)));
    }

    [Fact]
    public async Task BuiltTool_RunsAsProcess_ExitCodeAndOutput()
    {
        var dll = Path.Combine(AppContext.BaseDirectory, "Aibysitter.Cli.dll");
        var start = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { dll, "lint", "-", "--fail-on-error" },
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.StandardInput.Write(CliTests.Contradiction);
        process.StandardInput.Close();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await process.WaitForExitAsync(timeout.Token);
        var (output, error) = (await stdout, await stderr);

        Assert.Equal(CliApp.ThresholdFailed, process.ExitCode);
        Assert.StartsWith($"<stdin>: 90/100 A (Markdown rules file, ruleset v{RulesetVersion.Current})", output.ReplaceLineEndings("\n"));
        Assert.Equal("aibysitter: failed: 1 Error finding.", error.Trim());
    }
}
