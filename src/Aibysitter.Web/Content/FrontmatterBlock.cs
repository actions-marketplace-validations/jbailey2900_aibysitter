namespace Aibysitter.Web.Content;

/// <summary>Content-file metadata (notes, incidents): a leading <c>---</c> block of <c>key: value</c> lines. Values are trimmed; lines without a colon are ignored.</summary>
public static class FrontmatterBlock
{
    /// <returns>The keys and values, and the text after the closing <c>---</c>; null when the text has no frontmatter block.</returns>
    public static (Dictionary<string, string> Values, string Body)? Parse(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var end = Array.IndexOf(lines, "---", 1);
        if (lines[0] != "---" || end < 0)
        {
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in lines[1..end].Select(l => l.Split(':', 2)).Where(p => p.Length == 2))
        {
            values[pair[0].Trim()] = pair[1].Trim();
        }

        return (values, string.Join('\n', lines[(end + 1)..]));
    }
}
