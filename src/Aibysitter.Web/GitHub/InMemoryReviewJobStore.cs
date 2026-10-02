namespace Aibysitter.Web.GitHub;

/// <summary>No persistence: jobs live only in <see cref="ReviewQueue"/> and are lost on process exit.</summary>
public sealed class InMemoryReviewJobStore : IReviewJobStore
{
    public bool IsDurable => false;

    public StoredJobState Find(string? deliveryId, out ReviewJob? job)
    {
        job = null;
        return StoredJobState.None;
    }

    public void Save(ReviewJob job)
    {
    }

    public IReviewJobLease? TryAcquire(ReviewJob job) => new Lease();

    public IReadOnlyList<ReviewJob> LoadPending() => [];

    private sealed class Lease : IReviewJobLease
    {
        public int Attempts => 1;

        public void Complete()
        {
        }

        public void Dispose()
        {
        }
    }
}
