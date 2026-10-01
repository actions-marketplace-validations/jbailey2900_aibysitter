using System.Text.RegularExpressions;
using Aibysitter.Rules.PullRequests;

namespace Aibysitter.Rules.Repo;

/// <summary>
/// Paths, package scripts, make targets, and MSBuild targets a pull request removes or renames away.
/// A name removed and re-added in the same file (an edit) does not count.
/// </summary>
public sealed partial class RemovedIdentifiers
{
    private RemovedIdentifiers(HashSet<string> paths, HashSet<string> scripts, HashSet<string> makeTargets, HashSet<string> msBuildTargets)
    {
        Paths = paths;
        Scripts = scripts;
        MakeTargets = makeTargets;
        MsBuildTargets = msBuildTargets;
    }

    public IReadOnlySet<string> Paths { get; }
    public IReadOnlySet<string> Scripts { get; }
    public IReadOnlySet<string> MakeTargets { get; }
    public IReadOnlySet<string> MsBuildTargets { get; }

    public bool Any => Paths.Count + Scripts.Count + MakeTargets.Count + MsBuildTargets.Count > 0;

    /// <summary>True when the path, or a directory above it, was removed or renamed away.</summary>
    public bool CoversPath(string path)
    {
        var p = RepoSnapshot.Normalize(path);
        return Paths.Contains(p) || Paths.Any(r => r.StartsWith(p + "/", StringComparison.Ordinal) || p.StartsWith(r + "/", StringComparison.Ordinal));
    }

    public static RemovedIdentifiers From(IEnumerable<ChangedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var paths = new HashSet<string>(StringComparer.Ordinal);
        var scripts = new HashSet<string>(StringComparer.Ordinal);
        var make = new HashSet<string>(StringComparer.Ordinal);
        var msbuild = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            if (file.Status == FileChangeStatus.Removed)
            {
                paths.Add(RepoSnapshot.Normalize(file.Path));
            }
            else if (file.Status == FileChangeStatus.Renamed && file.PreviousPath is not null)
            {
                paths.Add(RepoSnapshot.Normalize(file.PreviousPath));
            }

            var name = file.Path[(file.Path.LastIndexOf('/') + 1)..];
            if (name == "package.json")
            {
                Collect(file, ScriptLineRegex(), scripts, sectionKey: "scripts");
            }
            else if (Manifests.MakefileNames.Contains(name))
            {
                Collect(file, MakeRuleLineRegex(), make);
            }
            else if (Manifests.IsMsBuildFile(file.Path))
            {
                Collect(file, MsBuildTargetLineRegex(), msbuild);
            }
        }

        return new RemovedIdentifiers(paths, scripts, make, msbuild);
    }

    private static void Collect(ChangedFile file, Regex regex, HashSet<string> into, string? sectionKey = null)
    {
        var removed = new HashSet<string>(StringComparer.Ordinal);
        var added = new HashSet<string>(StringComparer.Ordinal);
        string? section = null;
        int? lastOld = null, lastNew = null;

        foreach (var line in file.Lines)
        {
            if ((line.OldLine is { } o && lastOld is { } lo && o > lo + 1) || (line.NewLine is { } n && lastNew is { } ln && n > ln + 1))
            {
                section = null;
            }

            lastOld = line.OldLine ?? lastOld;
            lastNew = line.NewLine ?? lastNew;

            if (sectionKey is not null)
            {
                var open = ObjectOpenRegex().Match(line.Text);
                if (open.Success)
                {
                    section = open.Groups[1].Value;
                    continue;
                }

                if (ObjectCloseRegex().IsMatch(line.Text))
                {
                    section = string.Empty;
                    continue;
                }

                if (section != sectionKey)
                {
                    continue;
                }
            }

            if (line.Kind == DiffLineKind.Context)
            {
                continue;
            }

            foreach (Match m in regex.Matches(line.Text))
            {
                foreach (var v in m.Groups["name"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    (line.Kind == DiffLineKind.Removed ? removed : added).Add(v);
                }
            }
        }

        into.UnionWith(removed.Except(added));
    }

    [GeneratedRegex(@"^\s*""(?<name>[^""]+)""\s*:")]
    private static partial Regex ScriptLineRegex();

    [GeneratedRegex(@"^(?<name>[^\s:#=][^:=#]*?)\s*::?(?!=)")]
    private static partial Regex MakeRuleLineRegex();

    [GeneratedRegex(@"<Target\s+[^>]*?Name\s*=\s*""(?<name>[^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex MsBuildTargetLineRegex();

    [GeneratedRegex(@"^\s*""([^""]+)""\s*:\s*\{\s*$")]
    private static partial Regex ObjectOpenRegex();

    [GeneratedRegex(@"^\s*\},?\s*$")]
    private static partial Regex ObjectCloseRegex();
}
