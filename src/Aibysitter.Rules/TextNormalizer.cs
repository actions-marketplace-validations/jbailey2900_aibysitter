using System.Text.RegularExpressions;

namespace Aibysitter.Rules;

internal static partial class TextNormalizer
{
    /// <summary>Trims, lowercases, strips a leading list marker, collapses whitespace.</summary>
    public static string Normalize(string text)
    {
        var s = ListMarkerRegex().Replace(text.Trim(), string.Empty);
        return WhitespaceRegex().Replace(s, " ").Trim().ToLowerInvariant();
    }

    [GeneratedRegex(@"^(?:[-*+]|\d+[.)])\s+")]
    private static partial Regex ListMarkerRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
