using System.Threading.Channels;

namespace Aibysitter.Web.GitHub;

public sealed record ReviewJob(PullRequestRef PullRequest, long CheckRunId, string? DeliveryId);

/// <summary>In-memory review queue. Jobs are lost on process exit; their check runs stay queued (documented v1 limitation).</summary>
public sealed class ReviewQueue
{
    public const int Capacity = 1000;

    private readonly Channel<ReviewJob> channel = Channel.CreateBounded<ReviewJob>(new BoundedChannelOptions(Capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
    });

    public bool TryEnqueue(ReviewJob job) => channel.Writer.TryWrite(job);

    public IAsyncEnumerable<ReviewJob> ReadAllAsync(CancellationToken cancellationToken) => channel.Reader.ReadAllAsync(cancellationToken);
}
