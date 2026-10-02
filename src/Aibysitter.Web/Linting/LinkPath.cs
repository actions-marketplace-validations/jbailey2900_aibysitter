using System.Text.RegularExpressions;

namespace Aibysitter.Web.Linting;

/// <summary>
/// raw.githubusercontent.com serves a symlink as its target path. A body that is one relative path (containing "/" or
/// ending in ".md") is read as a link and resolved against the linking file's folder, inside the repository.
/// </summary>
public static partial class LinkPath
{
    private const int MaxLength = 300;

    /// <summary>True with the repository-relative target when <paramref name="content"/> looks like a symlink body.</summary>
    public static bool TryResolve(string fromFile, string content, out string target)
    {
        target = string.Empty;
        var body = content.TrimEnd('\r', '\n');
        if (body.Length is 0 or > MaxLength || !PathRegex().IsMatch(body)
            || !(body.Contains('/') || body.EndsWith(".md", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var segments = new List<string>(fromFile.Split('/')[..^1]);
        foreach (var segment in body.Split('/'))
        {
            if (segment is "" or ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    return false;
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        target = string.Join('/', segments);
        return segments.Count > 0;
    }

    [GeneratedRegex(@"^[A-Za-z0-9._-]+(?:/[A-Za-z0-9._-]+)*$")]
    private static partial Regex PathRegex();
}
