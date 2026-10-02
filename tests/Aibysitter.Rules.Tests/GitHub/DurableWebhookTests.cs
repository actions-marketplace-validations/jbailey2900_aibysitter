using System.Net;
using Aibysitter.Web.GitHub;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aibysitter.Rules.Tests.GitHub.WebhookTestData;

namespace Aibysitter.Rules.Tests.GitHub;

public sealed class DurableWebhookTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "aib-webhook-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NewDelivery_SavesJobFile_BeforeAccepting()
    {
        var delivery = Guid.NewGuid().ToString();
        var (client, services) = Create(new FakeGitHubGateway());

        var response = await client.SendAsync(Request("pull_request", PullRequestPayload(), deliveryId: delivery));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.True(File.Exists(Path.Combine(root, delivery + ".json")));
        var queued = await GitHubWebhookTests.TryDequeue(services);
        Assert.Equal(StoredJobState.Pending, Store(services).Find(delivery, out var saved));
        Assert.Equal(saved, queued);
    }

    [Fact]
    public async Task Redelivery_OfPendingJob_RequeuesAgainstExistingCheckRun()
    {
        var delivery = Guid.NewGuid().ToString();
        var fake = new FakeGitHubGateway();
        var (client, services) = Create(fake);
        await client.SendAsync(Request("pull_request", PullRequestPayload(), deliveryId: delivery));
        fake.NextCheckRunId = 888;

        var response = await client.SendAsync(Request("pull_request", PullRequestPayload(), deliveryId: delivery));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Single(fake.Calls, c => c.StartsWith("create", StringComparison.Ordinal));
        Assert.Equal(777, (await GitHubWebhookTests.TryDequeue(services))!.CheckRunId);
        Assert.Equal(777, (await GitHubWebhookTests.TryDequeue(services))!.CheckRunId);
    }

    [Fact]
    public async Task Redelivery_WhileInProgress_AcceptsWithNoAction()
    {
        var delivery = Guid.NewGuid().ToString();
        var fake = new FakeGitHubGateway();
        var (client, services) = Create(fake);
        await client.SendAsync(Request("pull_request", PullRequestPayload(), deliveryId: delivery));
        var job = (await GitHubWebhookTests.TryDequeue(services))!;
        using var lease = Store(services).TryAcquire(job);

        var response = await client.SendAsync(Request("pull_request", PullRequestPayload(), deliveryId: delivery));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Single(fake.Calls);
        Assert.Null(await GitHubWebhookTests.TryDequeue(services));
    }

    [Fact]
    public async Task SaveFails_ClosesCheckRun_Returns503_NothingQueued()
    {
        var fake = new FakeGitHubGateway();
        var (client, services) = Create(fake, s =>
        {
            s.RemoveAll<IReviewJobStore>();
            s.AddSingleton<IReviewJobStore>(new FailingStore());
        });

        var response = await client.SendAsync(Request("pull_request", PullRequestPayload()));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(new[] { "create jbailey2900/sandbox#7@0123456", "complete 777" }, fake.Calls);
        Assert.Equal("Review failed", (await fake.Completed.Task).Title);
        Assert.Null(await GitHubWebhookTests.TryDequeue(services));
    }

    [Fact]
    public async Task NonGuidDelivery_StoredUnderGeneratedGuid()
    {
        var (client, services) = Create(new FakeGitHubGateway());

        var response = await client.SendAsync(Request("pull_request", PullRequestPayload(), deliveryId: "not-a-guid"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var job = (await GitHubWebhookTests.TryDequeue(services))!;
        Assert.True(Guid.TryParse(job.DeliveryId, out _));
        Assert.True(File.Exists(Path.Combine(root, job.DeliveryId + ".json")));
    }

    [Fact]
    public async Task HostStart_ReviewsJobSavedByPreviousProcess()
    {
        var job = new ReviewJob(new PullRequestRef(42, "jbailey2900", "sandbox", 7, "0123456789abcdef"), 555, Guid.NewGuid().ToString("D"));
        new FileReviewJobStore(root, TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<FileReviewJobStore>.Instance).Save(job);
        var fake = new FakeGitHubGateway();

        var (_, services) = GitHubWebhookTests.Create(factory, fake, runWorker: true, queuePath: root);
        await fake.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Contains("complete 555", fake.Calls);
        Assert.DoesNotContain(fake.Calls, c => c.StartsWith("create", StringComparison.Ordinal));
        Assert.True(await Eventually(() => Store(services).Find(job.DeliveryId, out _) == StoredJobState.None), "job file not removed");
    }

    [Fact]
    public async Task NoQueuePath_UsesInMemoryStore()
    {
        var (_, services) = GitHubWebhookTests.Create(factory, new FakeGitHubGateway(), runWorker: false);

        Assert.IsType<InMemoryReviewJobStore>(Store(services));
    }

    private (HttpClient Client, IServiceProvider Services) Create(FakeGitHubGateway fake, Action<IServiceCollection>? configure = null) =>
        GitHubWebhookTests.Create(factory, fake, runWorker: false, queuePath: root, configure: configure);

    private static async Task<bool> Eventually(Func<bool> condition)
    {
        for (var i = 0; i < 50 && !condition(); i++)
        {
            await Task.Delay(50);
        }

        return condition();
    }

    private static IReviewJobStore Store(IServiceProvider services) => services.GetRequiredService<IReviewJobStore>();

    private sealed class FailingStore : IReviewJobStore
    {
        public bool IsDurable => true;

        public StoredJobState Find(string? deliveryId, out ReviewJob? job)
        {
            job = null;
            return StoredJobState.None;
        }

        public void Save(ReviewJob job) => throw new UnauthorizedAccessException("denied");

        public IReviewJobLease? TryAcquire(ReviewJob job) => null;

        public IReadOnlyList<ReviewJob> LoadPending() => [];
    }
}
