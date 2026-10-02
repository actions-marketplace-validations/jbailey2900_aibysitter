using System.Text;
using System.Text.RegularExpressions;

namespace Aibysitter.Rules.Browser;

/// <summary>
/// Translates a .NET regex pattern into an equivalent JavaScript pattern for the <c>u</c> flag.
/// <c>\w \W \d \D \s \S \b \B</c> are rewritten with Unicode properties so they match what .NET matches;
/// identity escapes that <c>u</c> mode rejects are unescaped. .NET-only constructs throw.
/// Known gap: .NET matches UTF-16 code units, so a character outside the BMP is never a word character there; in JavaScript it can be.
/// </summary>
public static class JsRegexTranslator
{
    /// <summary>.NET \w: letters, non-spacing marks, decimal digits, connector punctuation.</summary>
    private const string WordClass = @"\p{L}\p{Mn}\p{Nd}\p{Pc}";

    /// <summary>.NET \s: \f \n \r \t \v, U+0085, and separators.</summary>
    /// <summary>.NET \b: word characters plus ZWNJ and ZWJ.</summary>
    private const string BoundaryClass = WordClass + @"\u200C\u200D";

    private const string SpaceClass = @"\f\n\r\t\v\x85\p{Z}";

    private const string SyntaxChars = @"^$\.*+?()[]{}|/";

    public static (string Source, string Flags) Translate(string pattern, RegexOptions options)
    {
        var unsupported = options & ~(RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        if (unsupported != 0)
        {
            throw new NotSupportedException($"RegexOptions {unsupported} has no JavaScript equivalent: {pattern}");
        }

        var flags = new StringBuilder();
        if (options.HasFlag(RegexOptions.IgnoreCase))
        {
            flags.Append('i');
        }

        if (options.HasFlag(RegexOptions.Multiline))
        {
            flags.Append('m');
        }

        if (options.HasFlag(RegexOptions.Singleline))
        {
            flags.Append('s');
        }

        flags.Append('u');
        return (TranslateSource(pattern, options.HasFlag(RegexOptions.Singleline)), flags.ToString());
    }

    private static string TranslateSource(string p, bool singleline)
    {
        var sb = new StringBuilder();
        var inClass = false;
        for (var i = 0; i < p.Length; i++)
        {
            var c = p[i];

            if (c == '(' && i + 1 < p.Length && p[i + 1] == '?')
            {
                var rest = p[(i + 2)..];
                if (!inClass && (rest.StartsWith('>') || Regex.IsMatch(rest, @"^[imnsx-]+[:)]")))
                {
                    throw new NotSupportedException($"Atomic groups and inline options have no JavaScript equivalent: {p}");
                }
            }

            if (c == '[' && !inClass)
            {
                inClass = true;
                sb.Append(c);
                if (i + 1 < p.Length && p[i + 1] == '^')
                {
                    sb.Append('^');
                    i++;
                }

                // A leading ']' is a literal in .NET; JavaScript needs it escaped.
                if (i + 1 < p.Length && p[i + 1] == ']')
                {
                    sb.Append(@"\]");
                    i++;
                }

                continue;
            }

            if (c == ']' && inClass)
            {
                inClass = false;
                sb.Append(c);
                continue;
            }

            if (inClass && c == '[')
            {
                sb.Append(@"\[");
                continue;
            }

            if (!inClass && (c == '{' || c == '}') && !IsQuantifierBrace(p, i))
            {
                sb.Append('\\').Append(c);
                continue;
            }

            // .NET '.' excludes only \n; JavaScript '.' also excludes \r, U+2028, and U+2029.
            if (c == '.' && !inClass && !singleline)
            {
                sb.Append(@"[^\n]");
                continue;
            }

            if (c != '\\')
            {
                sb.Append(c);
                continue;
            }

            if (i + 1 >= p.Length)
            {
                throw new NotSupportedException($"Trailing backslash: {p}");
            }

            var n = p[++i];
            switch (n)
            {
                case 'w':
                    sb.Append(inClass ? WordClass : $"[{WordClass}]");
                    break;
                case 'W':
                    sb.Append(inClass ? throw new NotSupportedException($"\\W inside a class: {p}") : $"[^{WordClass}]");
                    break;
                case 'd':
                    sb.Append(@"\p{Nd}");
                    break;
                case 'D':
                    sb.Append(inClass ? throw new NotSupportedException($"\\D inside a class: {p}") : @"\P{Nd}");
                    break;
                case 's':
                    sb.Append(inClass ? SpaceClass : $"[{SpaceClass}]");
                    break;
                case 'S':
                    sb.Append(inClass ? throw new NotSupportedException($"\\S inside a class: {p}") : $"[^{SpaceClass}]");
                    break;
                case 'b' when !inClass:
                    sb.Append($"(?:(?<=[{BoundaryClass}])(?![{BoundaryClass}])|(?<![{BoundaryClass}])(?=[{BoundaryClass}]))");
                    break;
                case 'B' when !inClass:
                    sb.Append($"(?:(?<=[{BoundaryClass}])(?=[{BoundaryClass}])|(?<![{BoundaryClass}])(?![{BoundaryClass}]))");
                    break;
                case 'p' or 'P':
                    var close = p.IndexOf('}', i);
                    sb.Append('\\').Append(n).Append(p, i + 1, close - i);
                    i = close;
                    break;
                case 'A' or 'z' or 'Z' or 'G':
                    throw new NotSupportedException($"\\{n} has no JavaScript equivalent: {p}");
                case 't' or 'n' or 'r' or 'f' or 'v' or 'x' or 'u' or 'k' or '0' or 'b':
                    sb.Append('\\').Append(n);
                    break;
                case >= '1' and <= '9':
                    sb.Append('\\').Append(n);
                    break;
                default:
                    if (char.IsLetterOrDigit(n))
                    {
                        throw new NotSupportedException($"Escape \\{n} not handled: {p}");
                    }

                    // u mode allows identity escapes only for syntax characters (and '-' inside a class).
                    if (SyntaxChars.Contains(n) || (inClass && n == '-'))
                    {
                        sb.Append('\\').Append(n);
                    }
                    else
                    {
                        sb.Append(n);
                    }

                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>True when the brace at <paramref name="i"/> belongs to a {n}, {n,}, or {n,m} quantifier.</summary>
    private static bool IsQuantifierBrace(string p, int i)
    {
        if (p[i] == '{')
        {
            return Regex.IsMatch(p[i..], @"^\{\d+(?:,\d*)?\}");
        }

        var open = p.LastIndexOf('{', i);
        return open >= 0 && Regex.IsMatch(p[open..(i + 1)], @"^\{\d+(?:,\d*)?\}$") && (open == 0 || p[open - 1] != '\\');
    }
}
