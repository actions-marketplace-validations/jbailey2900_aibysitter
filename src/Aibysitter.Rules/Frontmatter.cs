using System.Text.RegularExpressions;

namespace Aibysitter.Rules;

/// <summary>
/// Leading YAML-style block: line 1 is <c>---</c>, closed by <c>---</c> or <c>...</c> within <see cref="MaxLines"/> lines.
/// Reads top-level <c>key: value</c> pairs; indented <c>- item</c> lines append to the previous key.
/// </summary>
public sealed partial class Frontmatter
{
    public const int MaxLines = 100;

    private readonly Dictionary<string, (int Line, List<string> Values)> entries;

    private Frontmatter(int endLine, Dictionary<string, (int, List<string>)> entries)
    {
        EndLine = endLine;
        this.entries = entries;
    }

    /// <summary>Line number of the closing delimiter.</summary>
    public int EndLine { get; }

    public IReadOnlyCollection<string> Keys => entries.Keys;

    public bool Has(string key) => entries.ContainsKey(key);

    /// <summary>Line number of a key, or 1 when absent.</summary>
    public int LineOf(string key) => entries.TryGetValue(key, out var e) ? e.Line : 1;

    /// <summary>Non-empty values of a key: the inline value and any list items, quotes removed.</summary>
    public IReadOnlyList<string> Values(string key) => entries.TryGetValue(key, out var e) ? e.Values : [];

    internal static Frontmatter? TryRead(IReadOnlyList<string> raw, int count)
    {
        if (count < 2 || raw[0].TrimEnd() != "---")
        {
            return null;
        }

        var entries = new Dictionary<string, (int, List<string>)>(StringComparer.Ordinal);
        string? last = null;
        for (var i = 1; i < Math.Min(count, MaxLines); i++)
        {
            var text = raw[i];
            if (text.TrimEnd() is "---" or "...")
            {
                return new Frontmatter(i + 1, entries);
            }

            var pair = PairRegex().Match(text);
            if (pair.Success)
            {
                last = pair.Groups["key"].Value;
                var values = SplitValue(pair.Groups["value"].Value);
                entries[last] = (i + 1, values);
                continue;
            }

            var item = ItemRegex().Match(text);
            if (item.Success && last is not null)
            {
                entries[last].Item2.AddRange(SplitValue(item.Groups["value"].Value));
            }
        }

        return null;
    }

    /// <summary>Splits a scalar or a flow list (<c>[a, b]</c>) or a comma list into non-empty unquoted values.</summary>
    private static List<string> SplitValue(string value)
    {
        var v = value.Trim();
        if (v.StartsWith('[') && v.EndsWith(']'))
        {
            v = v[1..^1];
        }

        return v.Split(',')
            .Select(s => s.Trim().Trim('"', '\''))
            .Where(s => s.Length > 0)
            .ToList();
    }

    [GeneratedRegex(@"^(?<key>[A-Za-z_][\w-]*)\s*:(?:\s+(?<value>.*))?\s*$")]
    private static partial Regex PairRegex();

    [GeneratedRegex(@"^\s+-\s+(?<value>.+)$")]
    private static partial Regex ItemRegex();
}
