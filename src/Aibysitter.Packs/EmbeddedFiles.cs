using System.Reflection;

namespace Aibysitter.Packs;

/// <summary>
/// Embedded resources under a prefix, keyed by the path below it with <c>/</c> separators. A Windows build writes
/// <c>%(RecursiveDir)</c> with <c>\</c>; a Linux build writes <c>/</c>.
/// </summary>
public static class EmbeddedFiles
{
    public static IReadOnlyDictionary<string, string> Read(Assembly assembly, string prefix) =>
        Select(assembly.GetManifestResourceNames(), prefix)
            .ToDictionary(r => r.Path, r => ReadResource(assembly, r.Resource), StringComparer.Ordinal);

    /// <returns>Path below <paramref name="prefix"/> with <c>/</c> separators, and the resource name as embedded.</returns>
    public static IEnumerable<(string Path, string Resource)> Select(IEnumerable<string> resourceNames, string prefix) =>
        resourceNames
            .Select(name => (Normalized: Normalize(name), Resource: name))
            .Where(r => r.Normalized.StartsWith(prefix, StringComparison.Ordinal))
            .Select(r => (r.Normalized[prefix.Length..], r.Resource));

    public static string Normalize(string path) => path.Replace('\\', '/');

    private static string ReadResource(Assembly assembly, string name)
    {
        using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
        return reader.ReadToEnd();
    }
}
