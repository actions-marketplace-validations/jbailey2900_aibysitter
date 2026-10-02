using System.Collections.Concurrent;
using Aibysitter.Web.Data;

namespace Aibysitter.Rules.Tests.Data;

internal sealed class FakeScoreHistory : IScoreHistory
{
    private static readonly DateTime Start = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    public ConcurrentQueue<ScoreRecord> Records { get; } = new();

    public bool Enabled => true;

    public Task RecordAsync(ScoreRecord record, CancellationToken cancellationToken)
    {
        Records.Enqueue(record);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ScorePoint>> RecentAsync(string repo, string fileName, int count, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ScorePoint>>(Records
            .Select((r, i) => (r, i))
            .Where(x => x.r.Repo == repo && x.r.FileName == fileName)
            .Reverse()
            .Take(count)
            .Select(x => new ScorePoint(Start.AddMinutes(x.i), x.r.RulesetVersion, x.r.Score, x.r.Grade))
            .ToList());

    public void Seed(string repo, string fileName, int count, int ruleset = 2)
    {
        for (var i = 0; i < count; i++)
        {
            Records.Enqueue(new ScoreRecord(repo, fileName, ruleset, 70 + i, "C", ScoreSource.Badge));
        }
    }
}
