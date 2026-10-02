namespace Aibysitter.Web.Data;

public enum ScoreSource : byte
{
    LintByUrl = 1,
    Badge = 2,
}

/// <summary>One lint-by-URL or badge result for a public repository. Row in <c>dbo.ScoreHistory</c>.</summary>
public sealed class ScoreHistoryEntry
{
    public long Id { get; set; }

    /// <summary><c>owner/repo</c>, lowercased.</summary>
    public required string Repo { get; set; }

    /// <summary>One of <see cref="Linting.RawGitHubFetcher.FileNames"/>.</summary>
    public required string FileName { get; set; }

    public short RulesetVersion { get; set; }

    public byte Score { get; set; }

    public required string Grade { get; set; }

    public ScoreSource Source { get; set; }

    public DateTime CreatedUtc { get; set; }
}
