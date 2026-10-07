using System.Net;
using Aibysitter.Web.GitHub;
using Microsoft.Extensions.Logging.Abstractions;
using Octokit;

namespace Aibysitter.Rules.Tests.GitHub;

public class CheckRunRetryTests
{
    private static NotFoundException NotFound() => new("Not Found", HttpStatusCode.NotFound);

    [Fact]
    public async Task NotFoundTwice_ThenSucceeds()
    {
        var time = new ImmediateTime();
        var calls = 0;

        await CheckRunRetry.RunAsync(() => ++calls <= 2 ? Task.FromException(NotFound()) : Task.CompletedTask, 1, time, NullLogger.Instance, CancellationToken.None);

        Assert.Equal(3, calls);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)], time.Delays);
    }

    [Fact]
    public async Task NotFoundEveryTime_ThrowsAfterThreeRetries()
    {
        var time = new ImmediateTime();
        var calls = 0;

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CheckRunRetry.RunAsync(() => { calls++; return Task.FromException(NotFound()); }, 1, time, NullLogger.Instance, CancellationToken.None));

        Assert.Equal(4, calls);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)], time.Delays);
    }

    [Fact]
    public async Task OtherErrors_NotRetried()
    {
        var time = new ImmediateTime();
        var calls = 0;

        await Assert.ThrowsAsync<ApiValidationException>(() =>
            CheckRunRetry.RunAsync(() => { calls++; return Task.FromException(new ApiValidationException()); }, 1, time, NullLogger.Instance, CancellationToken.None));

        Assert.Equal(1, calls);
        Assert.Empty(time.Delays);
    }
}
