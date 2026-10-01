using Aibysitter.Rules.Repo;

namespace Aibysitter.Rules.Rules;

/// <summary>
/// R006, GitHub App only: paths, package scripts, make targets, and MSBuild targets a rules file names that do not
/// exist at the pull request head. Not discovered by <see cref="LintEngine"/>: it needs a <see cref="RepoSnapshot"/>.
/// Paths need two or more segments and a first segment that exists beside the rules file or at the root.
/// Package scripts are checked against package.json beside the rules file and at the root; in a workspace repo
/// (workspaces field, pnpm-workspace.yaml, or several package.json files) a script found in neither is not reported.
/// References that cannot be resolved (no manifest, unreadable manifest) are skipped.
/// </summary>
public sealed class MissingIdentifiers
{
    public const string RuleId = "R006";
    public const int MaxMsBuildFiles = 30;

    public string Id => RuleId;
    public string Title => "Missing identifiers";
    public Severity Severity => Severity.Warning;

    /// <param name="removedOnly">When set, reports only references broken by these removals.</param>
    public IEnumerable<Finding> Evaluate(RulesFile file, string rulesFilePath, RepoSnapshot repo, RemovedIdentifiers? removedOnly = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(repo);

        var dir = RepoSnapshot.DirectoryOf(rulesFilePath);
        var refs = RepoReferences.Extract(file);
        var scripts = Lazy(() => IsWorkspaceRepo(repo) ? null : Union(Candidates(dir, "package.json").Select(p => Manifests.PackageScripts(repo.Read(p)))));
        var workspaceScripts = Lazy(() => IsWorkspaceRepo(repo) ? Union(Candidates(dir, "package.json").Select(p => Manifests.PackageScripts(repo.Read(p)))) : null);
        var makeTargets = Lazy(() => Union(Candidates(dir, Manifests.MakefileNames.ToArray()).Where(repo.FileExists).Take(1).Select(p => Manifests.MakeTargets(repo.Read(p)))));
        var msTargets = Lazy(() => MsBuildTargets(repo));
        var ignore = new GitIgnore();
        ignore.Add(repo.Read(".gitignore"), string.Empty);
        if (dir.Length > 0)
        {
            ignore.Add(repo.Read(RepoSnapshot.Combine(dir, ".gitignore")), dir);
        }

        foreach (var group in refs.GroupBy(r => r.Line).OrderBy(g => g.Key))
        {
            var missing = new List<string>();
            foreach (var r in group)
            {
                var message = r.Kind switch
                {
                    ReferenceKind.Path => CheckPath(r.Name, dir, repo, removedOnly, ignore),
                    ReferenceKind.PackageScript => scripts.Value is null && removedOnly is not null
                        ? CheckName(r.Name, workspaceScripts.Value, removedOnly.Scripts, $"package script `{r.Name}` not found in package.json")
                        : CheckName(r.Name, scripts.Value, removedOnly?.Scripts, $"package script `{r.Name}` not found in package.json"),
                    ReferenceKind.MakeTarget => CheckName(r.Name, makeTargets.Value, removedOnly?.MakeTargets, $"make target `{r.Name}` not found in the Makefile"),
                    ReferenceKind.MsBuildTarget => CheckName(r.Name, msTargets.Value, removedOnly?.MsBuildTargets, $"MSBuild target `{r.Name}` not found"),
                    _ => null,
                };

                if (message is not null)
                {
                    missing.Add(message);
                }
            }

            if (missing.Count > 0)
            {
                var text = string.Join("; ", missing.Distinct());
                yield return new Finding(RuleId, group.Key, char.ToUpperInvariant(text[0]) + text[1..] + ".", "Update the reference, or restore what it names.");
            }
        }
    }

    private static string? CheckPath(string written, string dir, RepoSnapshot repo, RemovedIdentifiers? removedOnly, GitIgnore ignore)
    {
        var path = RepoSnapshot.Normalize(written);
        if (!path.Contains('/'))
        {
            return null;
        }

        var relative = RepoSnapshot.Combine(dir, written);
        if (repo.ExistsIgnoreCase(relative) || repo.ExistsIgnoreCase(path)
            || repo.ExistsWithAnyExtension(relative) || repo.ExistsWithAnyExtension(path)
            || ignore.IsIgnored(relative) || ignore.IsIgnored(path))
        {
            return null;
        }

        // Extensionless path under a package folder is an export subpath (shared/browser), not a file.
        var firstDir = path.Split('/')[0];
        if (!path.Split('/')[^1].Contains('.') && (repo.FileExists($"{firstDir}/package.json") || repo.FileExists(RepoSnapshot.Combine(dir, $"{firstDir}/package.json"))))
        {
            return null;
        }

        if (removedOnly is not null)
        {
            return removedOnly.CoversPath(relative) || removedOnly.CoversPath(path) ? $"`{written}` not found at the PR head" : null;
        }

        // Skip text whose first segment is absent both beside the rules file and at the root: not a repo path.
        var first = path.Split('/')[0];
        return repo.Exists(RepoSnapshot.Combine(dir, first)) || repo.Exists(first)
            ? $"`{written}` not found at the PR head"
            : null;
    }

    private static string? CheckName(string name, IReadOnlySet<string>? declared, IReadOnlySet<string>? removed, string message)
    {
        if (declared is null || declared.Contains(name))
        {
            return null;
        }

        return removed is null || removed.Contains(name) ? message : null;
    }

    private static IEnumerable<string> Candidates(string dir, params string[] names) =>
        names.SelectMany(n => dir.Length == 0 ? new[] { n } : new[] { RepoSnapshot.Combine(dir, n), n }).Distinct();

    /// <summary>Union of readable sets; null when none was readable.</summary>
    private static IReadOnlySet<string>? Union(IEnumerable<IReadOnlySet<string>?> sets)
    {
        HashSet<string>? all = null;
        foreach (var s in sets.Where(s => s is not null))
        {
            (all ??= new HashSet<string>(StringComparer.Ordinal)).UnionWith(s!);
        }

        return all;
    }

    private static IReadOnlySet<string>? MsBuildTargets(RepoSnapshot repo)
    {
        var files = repo.Files.Where(Manifests.IsMsBuildFile).ToList();
        if (files.Count is 0 or > MaxMsBuildFiles)
        {
            return null;
        }

        var targets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in files)
        {
            var xml = repo.Read(f);
            if (xml is null)
            {
                return null;
            }

            targets.UnionWith(Manifests.MsBuildTargets(xml));
        }

        return targets;
    }

    private static Lazy<T> Lazy<T>(Func<T> f) => new(f);

    private static bool IsWorkspaceRepo(RepoSnapshot repo) =>
        repo.FileExists("pnpm-workspace.yaml")
        || repo.Files.Count(f => f == "package.json" || f.EndsWith("/package.json", StringComparison.Ordinal)) > 1
        || (repo.Read("package.json")?.Contains("\"workspaces\"", StringComparison.Ordinal) ?? false);

    /// <summary>Manifest paths <see cref="Evaluate"/> may read for this rules file, given the repo's file list.</summary>
    public static IEnumerable<string> ManifestsNeeded(RulesFile file, string rulesFilePath, RepoSnapshot repo)
    {
        var refs = RepoReferences.Extract(file);
        var dir = RepoSnapshot.DirectoryOf(rulesFilePath);
        IEnumerable<string> paths = refs.Any(r => r.Kind == ReferenceKind.Path) ? Candidates(dir, ".gitignore") : [];
        if (RepoReferences.Needs(refs, ReferenceKind.PackageScript))
        {
            paths = paths.Concat(Candidates(dir, "package.json"));
        }

        if (RepoReferences.Needs(refs, ReferenceKind.MakeTarget))
        {
            paths = paths.Concat(Candidates(dir, Manifests.MakefileNames.ToArray()));
        }

        if (RepoReferences.Needs(refs, ReferenceKind.MsBuildTarget))
        {
            var ms = repo.Files.Where(Manifests.IsMsBuildFile).ToList();
            if (ms.Count <= MaxMsBuildFiles)
            {
                paths = paths.Concat(ms);
            }
        }

        return paths.Where(repo.FileExists).Distinct();
    }
}
