using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

/// <summary>
/// Credential-like values on added lines of any file. Uses the same patterns as R009.
/// In code files, a connection-string password counts only inside a string literal. In CI config files, a
/// connection string to localhost, 127.0.0.1, (local) or . is a throwaway test credential and is not flagged.
/// </summary>
public sealed partial class SecretsInDiff : IPullRequestCheck
{
    private const string ConnectionPasswordKind = "Password in connection string";

    public string Id => "P005";
    public string Title => "Secrets in diff";
    public Severity Severity => Severity.Error;

    public IEnumerable<PullRequestFinding> Evaluate(PullRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var file in context.Files.Where(f => f.Status != FileChangeStatus.Removed))
        {
            var code = FileKinds.IsCode(file.Path);
            var hashComments = CodeText.UsesHashComments(file.Path);
            var ci = CiConfigEdited.IsCiConfig(file.Path);
            foreach (var line in file.AddedLines)
            {
                var matches = SecretPatterns.Find(line.Text)
                    .Where(m => !code || m.Kind != ConnectionPasswordKind || CodeText.IsInsideStringLiteral(line.Text, m.Column - 1, hashComments))
                    .Where(m => !ci || m.Kind != ConnectionPasswordKind || !LocalServerRegex().IsMatch(line.Text))
                    .ToList();
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

    [GeneratedRegex(@"\b(?:Server|Data\s+Source|Host|Address|Addr)\s*=\s*(?:tcp:)?(?:localhost|127\.0\.0\.1|\(local\)|\.)(?=\s*[,;:\\]|\s*$)", RegexOptions.IgnoreCase)]
    private static partial Regex LocalServerRegex();
}
