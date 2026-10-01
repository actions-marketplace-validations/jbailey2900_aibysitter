namespace Aibysitter.Rules.PullRequests;

/// <summary>Credential-like values on added lines of any file. Uses the same patterns as R009.</summary>
public sealed class SecretsInDiff : IPullRequestCheck
{
    public string Id => "P005";
    public string Title => "Secrets in diff";
    public Severity Severity => Severity.Error;

    public IEnumerable<PullRequestFinding> Evaluate(PullRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var file in context.Files.Where(f => f.Status != FileChangeStatus.Removed))
        {
            foreach (var line in file.AddedLines)
            {
                var matches = SecretPatterns.Find(line.Text);
                if (matches.Count > 0)
                {
                    yield return new PullRequestFinding(
                        Id,
                        file.Path,
                        line.NewLine!.Value,
                        $"Possible secret: {string.Join(", ", matches.Select(m => $"{m.Kind} ({m.Redacted})"))}",
                        "Remove the value and rotate it. Read it from configuration or a secret store.");
                }
            }
        }
    }
}
