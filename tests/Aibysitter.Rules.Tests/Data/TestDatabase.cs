using Aibysitter.Web.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Aibysitter.Rules.Tests.Data;

/// <summary>
/// A throwaway SQL Server database with the migrations applied. Server from AIBYSITTER_TEST_SQL.
/// Missing: fails when CI is set, otherwise the caller skips.
/// </summary>
internal sealed class TestDatabase : IAsyncDisposable
{
    public const string Variable = "AIBYSITTER_TEST_SQL";

    private TestDatabase(string connectionString) => ConnectionString = connectionString;

    public string ConnectionString { get; }

    public static async Task<TestDatabase?> CreateOrSkipAsync(ITestOutputHelper output)
    {
        var server = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(server))
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
            {
                Assert.Fail($"{Variable} is required for the SQL Server tests in CI and is not set.");
            }

            output.WriteLine($"SKIPPED: {Variable} not set; SQL Server tests not run.");
            return null;
        }

        var builder = new SqlConnectionStringBuilder(server) { InitialCatalog = "AibysitterTest_" + Guid.NewGuid().ToString("N") };
        var database = new TestDatabase(builder.ConnectionString);
        await using var db = database.Context();
        await db.Database.MigrateAsync();
        return database;
    }

    public AibysitterDbContext Context() =>
        new(new DbContextOptionsBuilder<AibysitterDbContext>().UseSqlServer(ConnectionString).Options);

    public IDbContextFactory<AibysitterDbContext> Factory() => new ContextFactory(this);

    public async ValueTask DisposeAsync()
    {
        await using var db = Context();
        await db.Database.EnsureDeletedAsync();
    }

    private sealed class ContextFactory(TestDatabase database) : IDbContextFactory<AibysitterDbContext>
    {
        public AibysitterDbContext CreateDbContext() => database.Context();
    }
}
