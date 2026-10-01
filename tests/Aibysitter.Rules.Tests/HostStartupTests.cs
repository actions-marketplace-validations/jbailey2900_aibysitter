using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests;

public class HostStartupTests
{
    private const int Rounds = 4;
    private const int HostsPerRound = 16;

    [Fact]
    public async Task ParallelStartup_AllHostsBuild()
    {
        var failures = new List<string>();

        for (var round = 0; round < Rounds; round++)
        {
            using var gate = new Barrier(HostsPerRound);
            var tasks = Enumerable.Range(0, HostsPerRound).Select(_ => Task.Factory.StartNew(
                () =>
                {
                    gate.SignalAndWait();
                    try
                    {
                        using var factory = new WebApplicationFactory<Program>();
                        factory.CreateClient().Dispose();
                        return null;
                    }
                    catch (InvalidOperationException ex)
                    {
                        return ex.Message;
                    }
                },
                TaskCreationOptions.LongRunning));

            failures.AddRange((await Task.WhenAll(tasks)).OfType<string>());
        }

        Assert.True(failures.Count == 0, $"{failures.Count} of {Rounds * HostsPerRound} hosts failed to start: {failures.FirstOrDefault()}");
    }
}
