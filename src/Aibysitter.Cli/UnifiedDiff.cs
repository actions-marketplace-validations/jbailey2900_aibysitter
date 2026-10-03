using System.Text;

namespace Aibysitter.Cli;

/// <summary>Line-based unified diff (LCS), <see cref="Context"/> lines of context. Lines compared without line endings.</summary>
internal static class UnifiedDiff
{
    public const int Context = 3;

    public static string Write(string label, string before, string after)
    {
        var a = Lines(before);
        var b = Lines(after);
        var ops = Edits(a, b);
        if (ops.All(o => o.Kind == ' '))
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        text.Append($"--- a/{label}\n+++ b/{label}\n");
        var i = 0;
        while (i < ops.Count)
        {
            if (ops[i].Kind == ' ')
            {
                i++;
                continue;
            }

            var start = Math.Max(0, i - Context);
            var end = i;
            while (end < ops.Count)
            {
                if (ops[end].Kind != ' ')
                {
                    end++;
                    continue;
                }

                var run = 0;
                while (end + run < ops.Count && ops[end + run].Kind == ' ')
                {
                    run++;
                }

                if (end + run >= ops.Count || run > 2 * Context)
                {
                    end += Math.Min(run, Context);
                    break;
                }

                end += run;
            }

            var hunk = ops.GetRange(start, end - start);
            var oldStart = ops[start].OldLine;
            var newStart = ops[start].NewLine;
            var oldCount = hunk.Count(o => o.Kind != '+');
            var newCount = hunk.Count(o => o.Kind != '-');
            text.Append($"@@ -{Range(oldStart, oldCount)} +{Range(newStart, newCount)} @@\n");
            foreach (var op in hunk)
            {
                text.Append(op.Kind).Append(op.Text).Append('\n');
            }

            i = end;
        }

        return text.ToString();
    }

    private static string Range(int start, int count) => count switch
    {
        0 => $"{start - 1},0",
        1 => $"{start}",
        _ => $"{start},{count}",
    };

    private static List<string> Lines(string text)
    {
        var lines = text.TrimStart('﻿').Replace("\r\n", "\n").Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    /// <param name="OldLine">1-based line in the old text this op is at (for '+', the next old line).</param>
    private sealed record Op(char Kind, string Text, int OldLine, int NewLine);

    private static List<Op> Edits(List<string> a, List<string> b)
    {
        var lcs = new int[a.Count + 1, b.Count + 1];
        for (var x = a.Count - 1; x >= 0; x--)
        {
            for (var y = b.Count - 1; y >= 0; y--)
            {
                lcs[x, y] = a[x] == b[y] ? lcs[x + 1, y + 1] + 1 : Math.Max(lcs[x + 1, y], lcs[x, y + 1]);
            }
        }

        var ops = new List<Op>();
        int i = 0, j = 0;
        while (i < a.Count || j < b.Count)
        {
            if (i < a.Count && j < b.Count && a[i] == b[j])
            {
                ops.Add(new Op(' ', a[i], i + 1, j + 1));
                i++;
                j++;
            }
            else if (j < b.Count && (i == a.Count || lcs[i, j + 1] >= lcs[i + 1, j]))
            {
                ops.Add(new Op('+', b[j], i + 1, j + 1));
                j++;
            }
            else
            {
                ops.Add(new Op('-', a[i], i + 1, j + 1));
                i++;
            }
        }

        return ops;
    }
}
