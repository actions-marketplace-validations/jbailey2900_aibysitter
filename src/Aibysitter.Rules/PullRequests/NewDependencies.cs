using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

/// <summary>
/// Packages added by the pull request: .csproj / .props PackageReference and PackageVersion, package.json dependency
/// sections, requirements*.txt, go.mod require. A name both removed and added in the same file (version change) is not new.
/// </summary>
public sealed partial class NewDependencies : IPullRequestCheck
{
    private static readonly HashSet<string> DependencySections = new(StringComparer.Ordinal)
    {
        "dependencies", "devDependencies", "peerDependencies", "optionalDependencies",
    };

    private static readonly HashSet<string> NonPackageKeys = new(StringComparer.Ordinal)
    {
        "name", "version", "node", "npm", "pnpm", "yarn", "bun",
    };

    public string Id => "P010";
    public string Title => "New dependencies";
    public Severity Severity => Severity.Warning;

    public IEnumerable<PullRequestFinding> Evaluate(PullRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var file in context.Files.Where(f => f.Status != FileChangeStatus.Removed))
        {
            var name = Path.GetFileName(file.Path);
            var extract = Extractor(name);
            if (extract is null)
            {
                continue;
            }

            var entries = extract(file.Lines).ToList();
            var removed = entries.Where(e => e.Line.Kind == DiffLineKind.Removed).Select(e => e.Package).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (line, package) in entries.Where(e => e.Line.Kind == DiffLineKind.Added && !removed.Contains(e.Package)))
            {
                yield return new PullRequestFinding(
                    Id,
                    file.Path,
                    line.NewLine!.Value,
                    $"New dependency: {package}",
                    "Confirm the package was requested, or remove it and use what the project already has.");
            }
        }
    }

    private static Func<IReadOnlyList<DiffLine>, IEnumerable<(DiffLine Line, string Package)>>? Extractor(string fileName) => fileName switch
    {
        _ when fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".props", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".targets", StringComparison.OrdinalIgnoreCase) => lines => ByRegex(lines, PackageReferenceRegex()),
        "package.json" => PackageJson,
        _ when RequirementsFileRegex().IsMatch(fileName) => lines => ByRegex(lines, RequirementRegex()),
        "go.mod" => lines => ByRegex(lines, GoRequireRegex()),
        _ => null,
    };

    private static IEnumerable<(DiffLine, string)> ByRegex(IReadOnlyList<DiffLine> lines, Regex regex) =>
        lines.Where(l => l.Kind != DiffLineKind.Context)
            .Select(l => (Line: l, Match: regex.Match(l.Text)))
            .Where(x => x.Match.Success)
            .Select(x => (x.Line, x.Match.Groups["name"].Value));

    /// <summary>
    /// Tracks the enclosing section from "key": { lines in the diff; resets at each hunk (line-number gap).
    /// Unknown section (hunk starts mid-object):
    /// accepts entries whose value looks like a version or package spec.
    /// </summary>
    private static IEnumerable<(DiffLine, string)> PackageJson(IReadOnlyList<DiffLine> lines)
    {
        string? section = null;
        int? lastOld = null, lastNew = null;
        foreach (var line in lines)
        {
            var newHunk = (line.OldLine is { } o && lastOld is { } lo && o > lo + 1) || (line.NewLine is { } n && lastNew is { } ln && n > ln + 1);
            lastOld = line.OldLine ?? lastOld;
            lastNew = line.NewLine ?? lastNew;
            if (newHunk)
            {
                section = null;
            }

            var open = ObjectOpenRegex().Match(line.Text);
            if (open.Success)
            {
                section = open.Groups["key"].Value;
                continue;
            }

            if (ObjectCloseRegex().IsMatch(line.Text))
            {
                section = string.Empty;
                continue;
            }

            if (line.Kind == DiffLineKind.Context)
            {
                continue;
            }

            var entry = JsonEntryRegex().Match(line.Text);
            if (!entry.Success)
            {
                continue;
            }

            var key = entry.Groups["name"].Value;
            var isDependency = section switch
            {
                null => !NonPackageKeys.Contains(key) && VersionSpecRegex().IsMatch(entry.Groups["value"].Value),
                _ => DependencySections.Contains(section),
            };

            if (isDependency)
            {
                yield return (line, key);
            }
        }
    }

    [GeneratedRegex(@"<Package(?:Reference|Version)\s+(?:[^>]*\s)?Include\s*=\s*""(?<name>[^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex PackageReferenceRegex();

    [GeneratedRegex(@"^requirements[\w.-]*\.txt$", RegexOptions.IgnoreCase)]
    private static partial Regex RequirementsFileRegex();

    [GeneratedRegex(@"^\s*(?<name>[A-Za-z0-9][A-Za-z0-9._-]*)\s*(?:\[[^\]]*\])?\s*(?:[=<>!~;@]|$)")]
    private static partial Regex RequirementRegex();

    [GeneratedRegex(@"^\s*(?:require\s+)?(?<name>[\w.-]+\.[\w.-]+/[\w./-]+)\s+v\d")]
    private static partial Regex GoRequireRegex();

    [GeneratedRegex(@"^\s*""(?<key>[^""]+)""\s*:\s*\{\s*$")]
    private static partial Regex ObjectOpenRegex();

    [GeneratedRegex(@"^\s*\},?\s*$")]
    private static partial Regex ObjectCloseRegex();

    [GeneratedRegex(@"^\s*""(?<name>@?[^""\s]+)""\s*:\s*""(?<value>[^""]*)""")]
    private static partial Regex JsonEntryRegex();

    [GeneratedRegex(@"^(?:[\^~<>=*]|\d|workspace:|npm:|file:|link:|git|https?:|latest$)")]
    private static partial Regex VersionSpecRegex();
}
