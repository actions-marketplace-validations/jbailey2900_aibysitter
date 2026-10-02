using Aibysitter.Web.Linting;
using Microsoft.EntityFrameworkCore;

namespace Aibysitter.Web.Data;

public sealed record ScoreRecord(string Repo, string FileName, int RulesetVersion, int Score, string Grade, ScoreSource Source);

public sealed record ScorePoint(DateTime CreatedUtc, int RulesetVersion, int Score, string Grade);

/// <summary>Score history for public repositories. Written only from lint by URL and badges, on a found file.</summary>
public interface IScoreHistory
{
    /// <summary>False when <c>ConnectionStrings:Aibysitter</c> is not set; nothing is read or written.</summary>
    bool Enabled { get; }

    /// <summary>Stores one row. Failures are logged without the repository name and not thrown.</summary>
    Task RecordAsync(ScoreRecord record, CancellationToken cancellationToken);

    /// <summary>Newest first. Empty on failure.</summary>
    Task<IReadOnlyList<ScorePoint>> RecentAsync(string repo, string fileName, int count, CancellationToken cancellationToken);
}

public static class ScoreHistoryKey
{
    /// <summary><c>owner/repo</c>, lowercased.</summary>
    public static string Repo(RepoRef repo) => repo.ToString().ToLowerInvariant();
}

public sealed class NullScoreHistory : IScoreHistory
{
    public bool Enabled => false;

    public Task RecordAsync(ScoreRecord record, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<ScorePoint>> RecentAsync(string repo, string fileName, int count, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ScorePoint>>([]);
}

public sealed class EfScoreHistory(IDbContextFactory<AibysitterDbContext> contexts, ILogger<EfScoreHistory> logger) : IScoreHistory
{
    public bool Enabled => true;

    public async Task RecordAsync(ScoreRecord record, CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await contexts.CreateDbContextAsync(cancellationToken);
            db.ScoreHistory.Add(new ScoreHistoryEntry
            {
                Repo = record.Repo,
                FileName = record.FileName,
                RulesetVersion = (short)record.RulesetVersion,
                Score = (byte)record.Score,
                Grade = record.Grade,
                Source = record.Source,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Score history write failed ({Source}): {Error}", record.Source, ex.GetType().Name);
        }
    }

    public async Task<IReadOnlyList<ScorePoint>> RecentAsync(string repo, string fileName, int count, CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await contexts.CreateDbContextAsync(cancellationToken);
            return await db.ScoreHistory.AsNoTracking()
                .Where(e => e.Repo == repo && e.FileName == fileName)
                .OrderByDescending(e => e.CreatedUtc).ThenByDescending(e => e.Id)
                .Take(count)
                .Select(e => new ScorePoint(DateTime.SpecifyKind(e.CreatedUtc, DateTimeKind.Utc), e.RulesetVersion, e.Score, e.Grade))
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Score history read failed: {Error}", ex.GetType().Name);
            return [];
        }
    }
}
