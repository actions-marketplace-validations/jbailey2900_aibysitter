using Aibysitter.Web.Data;
using Aibysitter.Web.Linting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aibysitter.Rules.Tests.Data;

public class ScoreHistoryModelTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public void Migrations_MatchModel()
    {
        using var db = new DesignTimeDbContextFactory().CreateDbContext([]);

        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void FileNameConstraint_ListsEveryFetchedFileName()
    {
        using var db = new DesignTimeDbContextFactory().CreateDbContext([]);
        var check = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ScoreHistoryEntry))!.GetCheckConstraints().Single(c => c.Name == "CK_ScoreHistory_FileName");

        Assert.All(RawGitHubFetcher.FileNames, name => Assert.Contains($"'{name}'", check.Sql));
    }

    [Fact]
    public void NoConnectionString_UsesNullStore_NoDbContext()
    {
        Assert.IsType<NullScoreHistory>(factory.Services.GetRequiredService<IScoreHistory>());
        Assert.Null(factory.Services.GetService<IDbContextFactory<AibysitterDbContext>>());
    }

    [Fact]
    public void ConnectionString_UsesEfStore_WithoutTouchingTheDatabase()
    {
        var app = factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Aibysitter", "Server=127.0.0.1,1;Database=None;User Id=x;Password=y;Connect Timeout=1"));

        Assert.IsType<EfScoreHistory>(app.Services.GetRequiredService<IScoreHistory>());
    }

    [Fact]
    public void AppCode_NeverDependsOnScoreHistory()
    {
        var storeTypes = new[] { typeof(IScoreHistory), typeof(AibysitterDbContext), typeof(IDbContextFactory<AibysitterDbContext>) };
        var offenders = typeof(Program).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Aibysitter.Web.GitHub", StringComparison.Ordinal) == true)
            .SelectMany(t => t.GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SelectMany(c => c.GetParameters())
                .Where(p => storeTypes.Contains(p.ParameterType))
                .Select(p => $"{t.Name}({p.ParameterType.Name})"))
            .ToList();

        Assert.Empty(offenders);
    }
}
