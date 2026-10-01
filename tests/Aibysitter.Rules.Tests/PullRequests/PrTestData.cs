using Aibysitter.Rules.PullRequests;

namespace Aibysitter.Rules.Tests.PullRequests;

internal static class PrTestData
{
    /// <summary>Builds an added file whose patch adds every given line starting at line 1.</summary>
    public static ChangedFile Added(string path, params string[] lines) =>
        new(path, FileChangeStatus.Added, $"@@ -0,0 +1,{lines.Length} @@\n" + string.Join("\n", lines.Select(l => "+" + l)), HeadContent: string.Join("\n", lines));

    public static ChangedFile FromPatchFixture(string path, string fixture, FileChangeStatus status = FileChangeStatus.Modified) =>
        new(path, status, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "patches", fixture)));

    public static PullRequestContext Context(params ChangedFile[] files) => new(files, RepoConfig.Default);

    public static PullRequestContext Context(RepoConfig config, params ChangedFile[] files) => new(files, config);
}
