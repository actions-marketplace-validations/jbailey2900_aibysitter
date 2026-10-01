namespace Aibysitter.Rules.PullRequests;

/// <summary>Test files removed by the pull request. Removed test methods inside kept files are not checked.</summary>
public sealed class DeletedTests : IPullRequestCheck
{
    public string Id => "P008";
    public string Title => "Deleted tests";
    public Severity Severity => Severity.Error;

    public IEnumerable<PullRequestFinding> Evaluate(PullRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var file in context.Files.Where(f => f.Status == FileChangeStatus.Removed && FileKinds.IsTestFile(f.Path)))
        {
            yield return new PullRequestFinding(
                Id,
                file.Path,
                1,
                $"Test file deleted: {file.Path}",
                "Restore the file, or state in the PR which tests replace it.");
        }
    }
}
