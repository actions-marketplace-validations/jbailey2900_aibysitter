using Aibysitter.Web.Linting;
using Microsoft.EntityFrameworkCore;

namespace Aibysitter.Web.Data;

public sealed class AibysitterDbContext(DbContextOptions<AibysitterDbContext> options) : DbContext(options)
{
    public const string ConnectionStringName = "Aibysitter";

    public DbSet<ScoreHistoryEntry> ScoreHistory => Set<ScoreHistoryEntry>();

    public DbSet<UsageStatEntry> UsageStats => Set<UsageStatEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entry = modelBuilder.Entity<ScoreHistoryEntry>();
        entry.ToTable("ScoreHistory", "dbo", t =>
        {
            t.HasCheckConstraint("CK_ScoreHistory_FileName", $"[FileName] IN ({string.Join(", ", RawGitHubFetcher.FileNames.Select(n => $"'{n}'"))})");
            t.HasCheckConstraint("CK_ScoreHistory_Score", "[Score] BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_ScoreHistory_Grade", "[Grade] IN ('A', 'B', 'C', 'D', 'F')");
            t.HasCheckConstraint("CK_ScoreHistory_Source", "[Source] IN (1, 2)");
        });
        entry.HasKey(e => e.Id);
        entry.Property(e => e.Id).UseIdentityColumn();
        entry.Property(e => e.Repo).HasMaxLength(140).IsUnicode(false);
        entry.Property(e => e.FileName).HasMaxLength(40).IsUnicode(false);
        entry.Property(e => e.Grade).HasMaxLength(1).IsFixedLength().IsUnicode(false);
        entry.Property(e => e.Source).HasConversion<byte>();
        entry.Property(e => e.CreatedUtc).HasColumnType("datetime2(0)").HasDefaultValueSql("sysutcdatetime()");
        entry.HasIndex(e => new { e.Repo, e.FileName, e.CreatedUtc })
            .HasDatabaseName("IX_ScoreHistory_Repo_File_Created")
            .IsDescending(false, false, true)
            .IncludeProperties(e => new { e.RulesetVersion, e.Score, e.Grade });

        var usage = modelBuilder.Entity<UsageStatEntry>();
        usage.ToTable("UsageStats", "dbo", t =>
            t.HasCheckConstraint("CK_UsageStats_Metric", $"[Metric] IN ({string.Join(", ", Stats.UsageMetric.All.Select(m => $"'{m}'"))})"));
        usage.HasKey(e => new { e.Date, e.Metric, e.Key });
        usage.Property(e => e.Date).HasColumnType("date");
        usage.Property(e => e.Metric).HasMaxLength(16).IsUnicode(false);
        usage.Property(e => e.Key).HasMaxLength(32).IsUnicode(false);
    }
}
