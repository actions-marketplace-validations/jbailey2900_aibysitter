namespace Aibysitter.Packs;

public static class MarkdownLines
{
    /// <summary>Each line with whether it is inside a ``` or ~~~ fenced block; fence delimiter lines count as inside.</summary>
    public static IEnumerable<(string Line, bool InFence)> WithFences(IEnumerable<string> lines)
    {
        string? fence = null;
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (fence is null && (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal)))
            {
                fence = trimmed[..3];
                yield return (line, true);
                continue;
            }

            if (fence is not null)
            {
                if (trimmed.StartsWith(fence, StringComparison.Ordinal))
                {
                    fence = null;
                }

                yield return (line, true);
                continue;
            }

            yield return (line, false);
        }
    }

    /// <summary>Lines not inside a ``` or ~~~ fenced block.</summary>
    public static IEnumerable<string> OutsideFences(IEnumerable<string> lines) =>
        WithFences(lines).Where(l => !l.InFence).Select(l => l.Line);
}
