namespace Aibysitter.Rules.PullRequests;

public static class FileKinds
{
    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".csx", ".cshtml", ".razor", ".vb", ".fs",
        ".js", ".jsx", ".mjs", ".cjs", ".ts", ".tsx",
        ".py", ".go", ".java", ".kt", ".rb", ".rs", ".php", ".swift",
        ".c", ".h", ".cpp", ".hpp", ".sql", ".ps1", ".psm1", ".sh",
    };

    private static readonly HashSet<string> ConfigExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".yml", ".yaml", ".xml", ".config", ".csproj", ".props", ".targets", ".env", ".toml", ".ini",
    };

    public static bool IsCode(string path) => CodeExtensions.Contains(Path.GetExtension(path));

    public static bool IsCodeOrConfig(string path) => IsCode(path) || ConfigExtensions.Contains(Path.GetExtension(path));
}
