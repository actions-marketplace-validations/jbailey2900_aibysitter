namespace Aibysitter.Web.GitHub;

/// <summary>State of a delivery's job in the store.</summary>
public enum StoredJobState
{
    /// <summary>No job for this delivery.</summary>
    None,

    /// <summary>Job saved and not being reviewed.</summary>
    Pending,

    /// <summary>Job being reviewed by this or another process.</summary>
    InProgress,
}

/// <summary>Where review jobs live until their check run is completed.</summary>
public interface IReviewJobStore
{
    public const int MaxAttempts = 3;

    bool IsDurable { get; }

    /// <summary>The stored state of a delivery's job, and the job when <see cref="StoredJobState.Pending"/>.</summary>
    StoredJobState Find(string? deliveryId, out ReviewJob? job);

    /// <summary>Saves a new job. Throws <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> on failure.</summary>
    void Save(ReviewJob job);

    /// <summary>Claims a job for review and counts the attempt. Null when the job is gone or claimed elsewhere.</summary>
    IReviewJobLease? TryAcquire(ReviewJob job);

    /// <summary>Saved jobs, oldest first. Unreadable jobs move aside; claimed jobs are skipped.</summary>
    IReadOnlyList<ReviewJob> LoadPending();
}

/// <summary>An exclusive claim on a stored job. Dispose without <see cref="Complete"/> to keep the job for a later run.</summary>
public interface IReviewJobLease : IDisposable
{
    /// <summary>Review attempts including this one.</summary>
    int Attempts { get; }

    /// <summary>Removes the job; its check run is completed.</summary>
    void Complete();
}
