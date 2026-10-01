using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

public static partial class PatchParser
{
    public static IReadOnlyList<DiffLine> Parse(string? patch)
    {
        if (string.IsNullOrEmpty(patch))
        {
            return [];
        }

        var result = new List<DiffLine>();
        int? oldLine = null;
        int? newLine = null;

        foreach (var raw in patch.Replace("\r\n", "\n").Split('\n'))
        {
            var header = HunkHeaderRegex().Match(raw);
            if (header.Success)
            {
                oldLine = int.Parse(header.Groups[1].Value);
                newLine = int.Parse(header.Groups[2].Value);
                continue;
            }

            if (oldLine is null || newLine is null || raw.Length == 0 || raw[0] == '\\')
            {
                continue;
            }

            var text = raw[1..];
            switch (raw[0])
            {
                case '+':
                    result.Add(new DiffLine(DiffLineKind.Added, null, newLine, text));
                    newLine++;
                    break;
                case '-':
                    result.Add(new DiffLine(DiffLineKind.Removed, oldLine, null, text));
                    oldLine++;
                    break;
                case ' ':
                    result.Add(new DiffLine(DiffLineKind.Context, oldLine, newLine, text));
                    oldLine++;
                    newLine++;
                    break;
            }
        }

        return result;
    }

    [GeneratedRegex(@"^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@")]
    private static partial Regex HunkHeaderRegex();
}
