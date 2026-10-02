namespace Aibysitter.Web.GitHub;

public sealed class ReviewQueueOptions
{
    public const string SectionName = "ReviewQueue";

    /// <summary>Folder for job files (IIS: ReviewQueue__Path). Not set: jobs are kept in memory only.</summary>
    public string? Path { get; set; }
}
