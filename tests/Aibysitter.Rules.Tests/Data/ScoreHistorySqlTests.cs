using Aibysitter.Web.Data;
using Aibysitter.Web.Linting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Aibysitter.Rules.Tests.Data;

/// <summary>Against SQL Server (AIBYSITTER_TEST_SQL); skipped locally without it, required in CI.</summary>
public class ScoreHistorySqlTests(ITestOutputHelper output)
{
    private static ScoreHistoryEntry Row(string repo, string file, DateTime created, int score = 90, string grade = "A") =>
        new() { Repo = repo, FileName = file, RulesetVersion = 2, Score = (byte)score, Grade = grade, Source = ScoreSource.LintByUrl, CreatedUtc = created };

    private static async Task InsertAsync(TestDatabase database, IEnumerable<ScoreHistoryEntry> rows)
    {
        await using var db = database.Context();
        db.ScoreHistory.AddRange(rows);
        await db.SaveChangesAsync();
    }

    private static async Task<int> CountAsync(TestDatabase database, string? repo = null)
    {
        await using var db = database.Context();
        return await db.ScoreHistory.CountAsync(e => repo == null || e.Repo == repo);
    }

    [Fact]
    public async Task Migration_CreatesTableIndexAndConstraints()
    {
        await using var database = await TestDatabase.CreateOrSkipAsync(output);
        if (database is null)
        {
            return;
        }

        await using var db = database.Context();
        var index = await db.Database.SqlQuery<string>(
            $"SELECT name AS [Value] FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.ScoreHistory') AND name = 'IX_ScoreHistory_Repo_File_Created'").ToListAsync();
        var checks = await db.Database.SqlQuery<string>(
            $"SELECT name AS [Value] FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.ScoreHistory') ORDER BY name").ToListAsync();

        Assert.Single(index);
        Assert.Equal(["CK_ScoreHistory_FileName", "CK_ScoreHistory_Grade", "CK_ScoreHistory_Score", "CK_ScoreHistory_Source"], checks);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Record_ThenRecent_NewestFirst_FilteredAndLimited()
    {
        await using var database = await TestDatabase.CreateOrSkipAsync(output);
        if (database is null)
        {
            return;
        }

        var history = new EfScoreHistory(database.Factory(), NullLogger<EfScoreHistory>.Instance);
        await InsertAsync(database, Enumerable.Range(0, 12).Select(i => Row("o/r", "CLAUDE.md", new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i), 80 + i, "B")));
        await InsertAsync(database, [Row("o/r", "AGENTS.md", DateTime.UtcNow), Row("o/other", "CLAUDE.md", DateTime.UtcNow)]);
        await history.RecordAsync(new ScoreRecord("o/r", "CLAUDE.md", 2, 100, "A", ScoreSource.Badge), CancellationToken.None);

        var recent = await history.RecentAsync("o/r", "CLAUDE.md", 10, CancellationToken.None);

        Assert.Equal(10, recent.Count);
        Assert.Equal(100, recent[0].Score);
        Assert.Equal(DateTimeKind.Utc, recent[0].CreatedUtc.Kind);
        Assert.True(DateTime.UtcNow - recent[0].CreatedUtc < TimeSpan.FromMinutes(5), "default CreatedUtc is the server's UTC time");
        Assert.Equal(Enumerable.Range(0, 9).Select(i => 91 - i), recent.Skip(1).Select(p => p.Score));
        await using var db = database.Context();
        Assert.Equal(ScoreSource.Badge, (await db.ScoreHistory.OrderByDescending(e => e.Id).FirstAsync()).Source);
    }

    [Theory]
    [InlineData("README.md", 90, "A", 1)]
    [InlineData("CLAUDE.md", 101, "A", 1)]
    [InlineData("CLAUDE.md", 90, "E", 1)]
    [InlineData("CLAUDE.md", 90, "A", 3)]
    public async Task CheckConstraints_RejectInvalidRows(string file, int score, string grade, int source)
    {
        await using var database = await TestDatabase.CreateOrSkipAsync(output);
        if (database is null)
        {
            return;
        }

        await using var db = database.Context();
        var ex = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlAsync(
            $"INSERT INTO dbo.ScoreHistory (Repo, FileName, RulesetVersion, Score, Grade, Source) VALUES ('o/r', {file}, 2, {score}, {grade}, {source})"));
        Assert.Equal(547, ex.Number);
    }

    [Fact]
    public async Task Purge_DeletesOlderThan400Days_InBatches()
    {
        await using var database = await TestDatabase.CreateOrSkipAsync(output);
        if (database is null)
        {
            return;
        }

        var now = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);
        await using (var db = database.Context())
        {
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO dbo.ScoreHistory (Repo, FileName, RulesetVersion, Score, Grade, Source, CreatedUtc)
                SELECT TOP (5001) CONCAT('o/r', ROW_NUMBER() OVER (ORDER BY (SELECT NULL))), 'CLAUDE.md', 2, 90, 'A', 1, {now.AddDays(-401)}
                FROM sys.all_objects a CROSS JOIN sys.all_objects b
                """);
        }

        await InsertAsync(database, [Row("o/keep", "CLAUDE.md", now.AddDays(-399))]);

        await using (var db = database.Context())
        {
            Assert.Equal(5001, await ScoreHistoryRetention.PurgeAsync(db, now, CancellationToken.None));
        }

        Assert.Equal(1, await CountAsync(database));
    }

    [Fact]
    public async Task Purge_KeepsNewest500PerRepoAndFile()
    {
        await using var database = await TestDatabase.CreateOrSkipAsync(output);
        if (database is null)
        {
            return;
        }

        var now = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);
        await InsertAsync(database, Enumerable.Range(0, 505).Select(i => Row("o/r", "CLAUDE.md", now.AddMinutes(-i), score: i % 101)));
        await InsertAsync(database, Enumerable.Range(0, 10).Select(i => Row("o/r", "AGENTS.md", now.AddMinutes(-i))));

        await using (var db = database.Context())
        {
            Assert.Equal(5, await ScoreHistoryRetention.PurgeAsync(db, now, CancellationToken.None));
            var oldest = await db.ScoreHistory.Where(e => e.FileName == "CLAUDE.md").MinAsync(e => e.CreatedUtc);
            Assert.Equal(now.AddMinutes(-499), oldest);
        }

        Assert.Equal(510, await CountAsync(database, "o/r"));
    }
}
