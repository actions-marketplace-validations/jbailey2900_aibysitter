using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Aibysitter.Rules;
using Aibysitter.Rules.PullRequests;
using Microsoft.Extensions.Options;
using Octokit;

namespace Aibysitter.Web.GitHub;

public sealed class OctokitGitHubGateway(IOptions<GitHubOptions> options, TimeProvider time) : IGitHubGateway, IDisposable
{
    private static readonly ProductHeaderValue Product = new("Aibysitter");
    private static readonly TimeSpan TokenRefreshMargin = TimeSpan.FromMinutes(5);

    private readonly Lazy<RSA> privateKey = new(() => AppJwt.LoadPrivateKey(options.Value.PrivateKeyPath!));
    private readonly ConcurrentDictionary<long, AccessToken> tokens = new();

    public async Task<long> CreateQueuedCheckRunAsync(PullRequestRef pr, CancellationToken cancellationToken)
    {
        var client = await ClientAsync(pr.InstallationId);
        var run = await client.Check.Run.Create(pr.Owner, pr.Repo, new NewCheckRun(GitHubOptions.CheckRunName, pr.HeadSha)
        {
            Status = CheckStatus.Queued,
        });
        return run.Id;
    }

    public async Task MarkInProgressAsync(PullRequestRef pr, long checkRunId, CancellationToken cancellationToken)
    {
        var client = await ClientAsync(pr.InstallationId);
        await client.Check.Run.Update(pr.Owner, pr.Repo, checkRunId, new CheckRunUpdate
        {
            Status = CheckStatus.InProgress,
            StartedAt = time.GetUtcNow(),
        });
    }

    public async Task<IReadOnlyList<ChangedFile>> GetChangedFilesAsync(PullRequestRef pr, CancellationToken cancellationToken)
    {
        var client = await ClientAsync(pr.InstallationId);
        var files = await client.PullRequest.Files(pr.Owner, pr.Repo, pr.Number);
        return files.Select(f => new ChangedFile(f.FileName, MapStatus(f.Status), f.Patch, f.PreviousFileName)).ToList();
    }

    public async Task<string?> GetFileContentAsync(PullRequestRef pr, string path, CancellationToken cancellationToken)
    {
        var client = await ClientAsync(pr.InstallationId);
        try
        {
            var bytes = await client.Repository.Content.GetRawContentByRef(pr.Owner, pr.Repo, path, pr.HeadSha);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    public async Task CompleteCheckRunAsync(PullRequestRef pr, long checkRunId, CheckRunReport report, CancellationToken cancellationToken)
    {
        var client = await ClientAsync(pr.InstallationId);
        var batches = report.AnnotationBatches().ToList();
        if (batches.Count == 0)
        {
            batches.Add([]);
        }

        for (var i = 0; i < batches.Count; i++)
        {
            var update = new CheckRunUpdate
            {
                Output = new NewCheckRunOutput(report.Title, report.Summary)
                {
                    Annotations = batches[i].Select(ToOctokit).ToList(),
                },
            };

            if (i == batches.Count - 1)
            {
                update.Status = CheckStatus.Completed;
                update.Conclusion = MapConclusion(report.Conclusion);
                update.CompletedAt = time.GetUtcNow();
            }

            await client.Check.Run.Update(pr.Owner, pr.Repo, checkRunId, update);
        }
    }

    public void Dispose()
    {
        if (privateKey.IsValueCreated)
        {
            privateKey.Value.Dispose();
        }
    }

    private async Task<IGitHubClient> ClientAsync(long installationId)
    {
        if (!tokens.TryGetValue(installationId, out var token) || token.ExpiresAt - time.GetUtcNow() < TokenRefreshMargin)
        {
            var appClient = new GitHubClient(Product)
            {
                Credentials = new Credentials(AppJwt.Create(options.Value.AppId, privateKey.Value, time.GetUtcNow()), AuthenticationType.Bearer),
            };
            token = await appClient.GitHubApps.CreateInstallationToken(installationId);
            tokens[installationId] = token;
        }

        return new GitHubClient(Product) { Credentials = new Credentials(token.Token) };
    }

    private static NewCheckRunAnnotation ToOctokit(CheckRunAnnotation a) =>
        new(a.Path, a.Line, a.Line, MapLevel(a.Severity), a.Message)
        {
            Title = a.Title,
            RawDetails = a.RawDetails,
        };

    internal static CheckAnnotationLevel MapLevel(Severity severity) => severity switch
    {
        Severity.Error => CheckAnnotationLevel.Failure,
        Severity.Warning => CheckAnnotationLevel.Warning,
        _ => CheckAnnotationLevel.Notice,
    };

    internal static CheckConclusion MapConclusion(ReviewConclusion conclusion) => conclusion switch
    {
        ReviewConclusion.Success => CheckConclusion.Success,
        ReviewConclusion.Failure => CheckConclusion.Failure,
        _ => CheckConclusion.Neutral,
    };

    internal static FileChangeStatus MapStatus(string? status) => status switch
    {
        "added" => FileChangeStatus.Added,
        "removed" => FileChangeStatus.Removed,
        "renamed" => FileChangeStatus.Renamed,
        "copied" => FileChangeStatus.Copied,
        "changed" => FileChangeStatus.Changed,
        "unchanged" => FileChangeStatus.Unchanged,
        _ => FileChangeStatus.Modified,
    };
}
