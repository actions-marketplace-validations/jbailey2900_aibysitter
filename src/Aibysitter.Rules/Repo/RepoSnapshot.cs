namespace Aibysitter.Rules.Repo;

/// <summary>Files at one commit: the full path list, and file text where it was read.</summary>
public sealed class RepoSnapshot
{
    private readonly HashSet<string> files;
    private readonly HashSet<string> directories;
    private readonly HashSet<string> filesIgnoreCase;
    private readonly HashSet<string> directoriesIgnoreCase;
    private readonly HashSet<string> fileNames;
    private readonly Func<string, string?> read;

    public RepoSnapshot(IEnumerable<string> paths, Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(paths);
        this.read = read ?? throw new ArgumentNullException(nameof(read));
        files = new HashSet<string>(paths.Select(Normalize), StringComparer.Ordinal);
        directories = new HashSet<string>(StringComparer.Ordinal);
        fileNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in files)
        {
            fileNames.Add(f[(f.LastIndexOf('/') + 1)..]);
            for (var i = f.IndexOf('/'); i >= 0; i = f.IndexOf('/', i + 1))
            {
                directories.Add(f[..i]);
            }
        }

        filesIgnoreCase = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
        directoriesIgnoreCase = new HashSet<string>(directories, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Exists, ignoring case.</summary>
    public bool ExistsIgnoreCase(string path)
    {
        var p = Normalize(path);
        return p.Length == 0 || filesIgnoreCase.Contains(p) || directoriesIgnoreCase.Contains(p);
    }

    /// <summary>True when a file exists at the path with an extension appended (<c>scripts/build</c> → <c>scripts/build.js</c>).</summary>
    public bool ExistsWithAnyExtension(string path)
    {
        var p = Normalize(path) + ".";
        return files.Any(f => f.StartsWith(p, StringComparison.Ordinal) && f.IndexOf('/', p.Length) < 0);
    }

    public IReadOnlyCollection<string> Files => files;

    public bool FileExists(string path) => files.Contains(Normalize(path));

    /// <summary>True for a file or a directory that contains files.</summary>
    public bool Exists(string path)
    {
        var p = Normalize(path);
        return p.Length == 0 || files.Contains(p) || directories.Contains(p);
    }

    public bool AnyFileNamed(string name) => fileNames.Contains(name);

    /// <summary>File text, or null when it was not read or does not exist.</summary>
    public string? Read(string path) => FileExists(path) ? read(Normalize(path)) : null;

    public static string Normalize(string path)
    {
        var parts = new List<string>();
        foreach (var segment in path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (parts.Count > 0)
                {
                    parts.RemoveAt(parts.Count - 1);
                }

                continue;
            }

            parts.Add(segment);
        }

        return string.Join('/', parts);
    }

    public static string DirectoryOf(string path)
    {
        var p = Normalize(path);
        var i = p.LastIndexOf('/');
        return i < 0 ? string.Empty : p[..i];
    }

    public static string Combine(string directory, string path) => Normalize(directory.Length == 0 ? path : $"{directory}/{path}");
}
