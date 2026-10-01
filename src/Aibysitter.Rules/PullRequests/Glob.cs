using System.Text;
using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

/// <summary>
/// Path glob anchored at the repo root, '/' separated, case-sensitive.
/// '**' matches any number of segments (including none), '*' any run within a segment, '?' one character within a segment.
/// </summary>
public sealed class Glob
{
    private readonly Regex regex;

    public Glob(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        Pattern = pattern.Trim().TrimStart('/');
        regex = new Regex(ToRegex(Pattern), RegexOptions.CultureInvariant);
    }

    public string Pattern { get; }

    public bool IsMatch(string path) => regex.IsMatch(path.TrimStart('/'));

    private static string ToRegex(string pattern)
    {
        var sb = new StringBuilder("^");
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '*' && i + 1 < pattern.Length && pattern[i + 1] == '*')
            {
                var atSegmentStart = i == 0 || pattern[i - 1] == '/';
                var followedBySlash = i + 2 < pattern.Length && pattern[i + 2] == '/';
                var atEnd = i + 2 == pattern.Length;

                if (atSegmentStart && followedBySlash)
                {
                    sb.Append("(?:.*/)?");
                    i += 2;
                }
                else if (atSegmentStart && atEnd && i > 0)
                {
                    sb.Length -= 1;
                    sb.Append("(?:/.*)?");
                    i += 1;
                }
                else
                {
                    sb.Append(".*");
                    i += 1;
                }
            }
            else if (c == '*')
            {
                sb.Append("[^/]*");
            }
            else if (c == '?')
            {
                sb.Append("[^/]");
            }
            else
            {
                sb.Append(Regex.Escape(c.ToString()));
            }
        }

        return sb.Append('$').ToString();
    }
}
