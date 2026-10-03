using Aibysitter.Rules;
using Aibysitter.Rules.PullRequests;
using Aibysitter.Rules.Repo;
using Aibysitter.Rules.Rules;

namespace Aibysitter.Web.GitHub;

public sealed class ReviewProcessor(IGitHubGateway gateway, PullRequestReviewer reviewer, ILogger<ReviewProcessor> logger, Stats.IUsageCounter? usage = null)
{
    public const int MaxContentFetches = 100;

    /// <summary>Unchanged rules files read for R006 when the PR removes or renames something.</summary>
    public const int MaxUnchangedRulesFiles = 10;

    /// <summary>package.json, Makefile, .gitignore, and MSBuild files read for R006.</summary>
    public const int MaxManifestFetches = 20;

    public async Task ProcessAsync(ReviewJob job, CancellationToken cancellationToken)
    {
        var pr = job.PullRequest;
        try
        {
            await gateway.MarkInProgressAsync(pr, job.CheckRunId, cancellationToken);

            var files = await gateway.GetChangedFilesAsync(pr, cancellationToken);
            var (config, configErrors) = RepoConfig.Parse(await gateway.GetFileContentAsync(pr, RepoConfig.FilePath, cancellationToken));

            var notes = new List<string>();
            var tree = await TreeForRulesFilesAsync(pr, files, config, cancellationToken);
            var symlinks = files.Where(f => RulesFileLint.IsRulesFile(f) && tree?.Symlinks.Contains(f.Path) == true).ToList();
            notes.AddRange(symlinks.Select(SymlinkNote));

            // Rules files are fetched first so the shared cap never starves P014. Symlinked rules files are not fetched:
            // GitHub returns the target's content under the link's path.
            var toFetch = files.Where(NeedsHeadContent)
                .Where(f => !symlinks.Contains(f))
                .OrderBy(f => RulesFileLint.IsRulesFile(f) ? 0 : 1)
                .Take(MaxContentFetches)
                .Select(f => f.Path)
                .ToHashSet(StringComparer.Ordinal);

            var enriched = new List<ChangedFile>(files.Count);
            foreach (var file in files)
            {
                enriched.Add(toFetch.Contains(file.Path)
                    ? file with { HeadContent = await gateway.GetFileContentAsync(pr, file.Path, cancellationToken) }
                    : file);
            }

            var (repo, unchanged) = await BuildRepoViewAsync(pr, files, enriched, config, tree, notes, cancellationToken);

            var review = reviewer.Review(new PullRequestContext(enriched, config, repo, unchanged));
            var report = CheckRunReport.Build(review, reviewer.Checks, enriched, config, configErrors, notes);
            if (config.Comment)
            {
                var note = await new ReviewCommentPublisher(gateway, logger)
                    .PublishAsync(pr, ReviewComment.Build(report, pr, job.CheckRunId), review.Findings.Count > 0, cancellationToken);
                if (note is not null)
                {
                    report = report with { Summary = $"{report.Summary}\n\n{note}" };
                }
            }

            await gateway.CompleteCheckRunAsync(pr, job.CheckRunId, report, cancellationToken);
            usage?.Increment(Stats.UsageMetric.Review, report.Conclusion.ToString().ToLowerInvariant());

            logger.LogInformation(
                "Reviewed {PullRequest} (delivery {DeliveryId}): {FindingCount} findings, {Conclusion}",
                pr, job.DeliveryId, review.Findings.Count, review.Conclusion);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Review of {PullRequest} (delivery {DeliveryId}) failed", pr, job.DeliveryId);
            try
            {
                await gateway.CompleteCheckRunAsync(pr, job.CheckRunId, CheckRunReport.ForError(ex), cancellationToken);
                usage?.Increment(Stats.UsageMetric.Review, "error");
            }
            catch (Exception closeEx)
            {
                logger.LogError(closeEx, "Could not close check run {CheckRunId} for {PullRequest}", job.CheckRunId, pr);
            }
        }
    }

    /// <summary>The head tree when P014 is enabled and the PR adds or changes a rules file; used to find symlinks.</summary>
    private async Task<RepoTree?> TreeForRulesFilesAsync(PullRequestRef pr, IReadOnlyList<ChangedFile> files, RepoConfig config, CancellationToken cancellationToken) =>
        config.IsEnabled(RulesFileLint.CheckId) && files.Any(RulesFileLint.IsRulesFile)
            ? await gateway.GetTreeAsync(pr, cancellationToken)
            : null;

    internal static string SymlinkNote(ChangedFile file) =>
        file.AddedLines.FirstOrDefault()?.Text.Trim() is { Length: > 0 } target
            ? $"P014 skipped {file.Path}: symlink to {target}."
            : $"P014 skipped {file.Path}: symlink.";

    /// <summary>
    /// File list and the few files R006 needs. Fetched only when P014 and R006 are enabled and the PR changes a rules
    /// file or removes / renames something.
    /// </summary>
    private async Task<(RepoSnapshot? Repo, IReadOnlyList<ChangedFile> Unchanged)> BuildRepoViewAsync(
        PullRequestRef pr,
        IReadOnlyList<ChangedFile> files,
        IReadOnlyList<ChangedFile> enriched,
        RepoConfig config,
        RepoTree? tree,
        List<string> notes,
        CancellationToken cancellationToken)
    {
        if (!config.IsEnabled(RulesFileLint.CheckId) || !config.IsEnabled(MissingIdentifiers.RuleId))
        {
            return (null, []);
        }

        var changedRules = enriched.Where(f => RulesFileLint.IsRulesFile(f) && f.HeadContent is not null).ToList();
        var removed = RemovedIdentifiers.From(files);
        if (changedRules.Count == 0 && !removed.Any)
        {
            return (null, []);
        }

        tree ??= changedRules.Count == 0 ? await gateway.GetTreeAsync(pr, cancellationToken) : null;
        var paths = tree?.Paths;
        if (paths is null)
        {
            notes.Add("R006 skipped: the repository file list is too large for GitHub to return in full.");
            return (null, []);
        }

        var contents = changedRules.ToDictionary(f => f.Path, f => f.HeadContent, StringComparer.Ordinal);
        var changedPaths = files.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);

        var unchanged = new List<ChangedFile>();
        if (removed.Any)
        {
            foreach (var path in paths.Where(p => !changedPaths.Contains(p) && !tree!.Symlinks.Contains(p) && RulesFormats.FromFileName(p) is not null)
                .OrderBy(p => p.Count(c => c == '/')).ThenBy(p => p, StringComparer.Ordinal).Take(MaxUnchangedRulesFiles))
            {
                var text = await gateway.GetFileContentAsync(pr, path, cancellationToken);
                if (text is not null)
                {
                    contents[path] = text;
                    unchanged.Add(new ChangedFile(path, FileChangeStatus.Unchanged, HeadContent: text));
                }
            }
        }

        var planning = new RepoSnapshot(paths, p => contents.GetValueOrDefault(p));
        var needed = changedRules.Concat(unchanged)
            .SelectMany(f => MissingIdentifiers.ManifestsNeeded(RulesFile.Parse(f.HeadContent!, RulesFormats.FromFileName(f.Path)!.Value), f.Path, planning))
            .Where(p => !contents.ContainsKey(p))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (needed.Count > MaxManifestFetches)
        {
            notes.Add($"R006: read {MaxManifestFetches} of {needed.Count} manifest files; references needing the rest were not checked.");
        }

        foreach (var path in needed.Take(MaxManifestFetches))
        {
            contents[path] = await gateway.GetFileContentAsync(pr, path, cancellationToken);
        }

        return (new RepoSnapshot(paths, p => contents.GetValueOrDefault(p)), unchanged);
    }

    internal static bool NeedsHeadContent(ChangedFile file) =>
        RulesFileLint.IsRulesFile(file)
        || (file.Status != FileChangeStatus.Removed
            && file.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            && (file.Path.Contains("test", StringComparison.OrdinalIgnoreCase) || (file.Patch?.Contains('[') ?? false)));
}
