using Aibysitter.Rules;

namespace Aibysitter.Cli;

/// <summary>Format from a filesystem path: <see cref="RulesFormats.FromFileName"/> on the path's last 1, 2, … segments.</summary>
internal static class PathFormat
{
    public static RulesFormat? Resolve(string path)
    {
        var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => s != ".")
            .ToList();
        for (var take = 1; take <= segments.Count; take++)
        {
            if (RulesFormats.FromFileName(string.Join('/', segments[^take..])) is { } format)
            {
                return format;
            }
        }

        return null;
    }
}
