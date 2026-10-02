using Aibysitter.Rules.Tests.Parity;
using Microsoft.Data.SqlClient;
using Xunit.Abstractions;

namespace Aibysitter.Rules.Tests.Data;

/// <summary>
/// Runs deploy/create-database.sql against SQL Server (AIBYSITTER_TEST_SQL). Only the DECLARE values are changed:
/// a throwaway database, account names containing ']' and spaces, and SQL logins (the Linux container has no Windows accounts).
/// </summary>
public class CreateDatabaseScriptTests(ITestOutputHelper output)
{
    private const string DatabaseLine = "DECLARE @Database sysname = N'Aibysitter';";
    private const string RunnerLine = "DECLARE @RunnerAccount sysname = N'::RUNNER_ACCOUNT::';";
    private const string AppPoolLine = @"DECLARE @AppPoolAccount sysname = N'IIS APPPOOL\aibysitting.net';";
    private const string LoginSourceLine = "DECLARE @LoginSource nvarchar(200) = N'FROM WINDOWS';";

    private static readonly string ScriptPath = Path.Combine(NodeRunner.RepoRoot, "deploy", "create-database.sql");

    private static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";

    private static string Script(string database, string runner, string appPool)
    {
        var script = File.ReadAllText(ScriptPath);
        foreach (var line in new[] { DatabaseLine, RunnerLine, AppPoolLine, LoginSourceLine })
        {
            Assert.Contains(line, script);
        }

        return script
            .Replace(DatabaseLine, $"DECLARE @Database sysname = {Literal(database)};")
            .Replace(RunnerLine, $"DECLARE @RunnerAccount sysname = {Literal(runner)};")
            .Replace(AppPoolLine, $"DECLARE @AppPoolAccount sysname = {Literal(appPool)};")
            .Replace(LoginSourceLine, "DECLARE @LoginSource nvarchar(200) = N'WITH PASSWORD = N''Test-only-Passw0rd!'', CHECK_POLICY = OFF';");
    }

    private static string? Server(ITestOutputHelper output)
    {
        var server = Environment.GetEnvironmentVariable(TestDatabase.Variable);
        if (!string.IsNullOrWhiteSpace(server))
        {
            return new SqlConnectionStringBuilder(server) { InitialCatalog = "master" }.ConnectionString;
        }

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
        {
            Assert.Fail($"{TestDatabase.Variable} is required for the SQL Server tests in CI and is not set.");
        }

        output.WriteLine($"SKIPPED: {TestDatabase.Variable} not set; SQL Server tests not run.");
        return null;
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountAsync(SqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Script_CreatesDatabaseLoginsUsersAndRoles_AndIsReRunnable()
    {
        if (Server(output) is not { } master)
        {
            return;
        }

        var id = Guid.NewGuid().ToString("N")[..8];
        var database = $"AibysitterSetup_{id}";
        var runner = $"aib runner]{id}";
        var appPool = $"aib app pool {id}";
        var script = Script(database, runner, appPool);

        await using var connection = new SqlConnection(master);
        await connection.OpenAsync();
        try
        {
            await ExecuteAsync(connection, script);
            await ExecuteAsync(connection, script);

            Assert.Equal(1, await CountAsync(connection, "SELECT COUNT(*) FROM sys.databases WHERE name = @db", ("@db", database)));
            Assert.Equal(2, await CountAsync(connection, "SELECT COUNT(*) FROM sys.server_principals WHERE name IN (@r, @a)", ("@r", runner), ("@a", appPool)));

            var memberships = $"""
                SELECT COUNT(*) FROM [{database}].sys.database_role_members m
                JOIN [{database}].sys.database_principals r ON r.principal_id = m.role_principal_id
                JOIN [{database}].sys.database_principals u ON u.principal_id = m.member_principal_id
                WHERE u.name = @user AND r.name = @role
                """;
            Assert.Equal(1, await CountAsync(connection, memberships, ("@user", runner), ("@role", "db_owner")));
            Assert.Equal(1, await CountAsync(connection, memberships, ("@user", appPool), ("@role", "db_datareader")));
            Assert.Equal(1, await CountAsync(connection, memberships, ("@user", appPool), ("@role", "db_datawriter")));
            Assert.Equal(0, await CountAsync(connection, memberships, ("@user", appPool), ("@role", "db_owner")));
        }
        finally
        {
            await ExecuteAsync(connection, $"""
                IF DB_ID(N'{database}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{database}];
                END;
                IF SUSER_ID({Literal(runner)}) IS NOT NULL DROP LOGIN [{runner.Replace("]", "]]")}];
                IF SUSER_ID({Literal(appPool)}) IS NOT NULL DROP LOGIN [{appPool}];
                """);
        }
    }

    [Fact]
    public async Task Script_Unedited_StopsBeforeCreatingAnything()
    {
        if (Server(output) is not { } master)
        {
            return;
        }

        var database = $"AibysitterSetup_{Guid.NewGuid().ToString("N")[..8]}";
        var script = File.ReadAllText(ScriptPath).Replace(DatabaseLine, $"DECLARE @Database sysname = {Literal(database)};");

        await using var connection = new SqlConnection(master);
        await connection.OpenAsync();
        var ex = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(connection, script));

        Assert.Equal(50000, ex.Number);
        Assert.Contains("Set @RunnerAccount before running this script.", ex.Message);
        Assert.Equal(0, await CountAsync(connection, "SELECT COUNT(*) FROM sys.databases WHERE name = @db", ("@db", database)));
    }

    [Fact]
    public void Script_HasNoBatchSeparators_AndNoInlineQuoteNameInExec()
    {
        var lines = File.ReadAllLines(ScriptPath);

        Assert.DoesNotContain(lines, l => l.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(lines, l => l.Contains("EXEC (", StringComparison.OrdinalIgnoreCase));
    }
}
