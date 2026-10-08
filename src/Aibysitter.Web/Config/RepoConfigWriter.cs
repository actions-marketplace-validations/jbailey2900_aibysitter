using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Aibysitter.Rules.PullRequests;

namespace Aibysitter.Web.Config;

/// <summary>
/// Writes <c>.github/aibysitter.json</c>: keys in the order scope, conclusion, disable, ignore, comment; 2-space indent; LF line ends.
/// <c>conclusion</c> is always written; <c>scope</c>, <c>disable</c> and <c>ignore</c> only when non-empty; <c>comment</c> only when true.
/// </summary>
public static class RepoConfigWriter
{
    private static readonly JsonWriterOptions Options = new()
    {
        Indented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string ConclusionValue(ConclusionMode mode) => RepoConfig.ConclusionName(mode);

    /// <summary>One glob per line; lines trimmed, blank lines and repeats dropped, order kept.</summary>
    public static IReadOnlyList<string> ScopeLines(string? text) =>
        (text ?? string.Empty).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Distinct(StringComparer.Ordinal).ToList();

    public static string Write(IReadOnlyList<string> scope, ConclusionMode conclusion, IReadOnlyList<string> disable, bool comment = false, IReadOnlyList<string>? ignore = null)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, Options))
        {
            writer.WriteStartObject();
            if (scope.Count > 0)
            {
                WriteArray(writer, "scope", scope);
            }

            writer.WriteString("conclusion", ConclusionValue(conclusion));
            if (disable.Count > 0)
            {
                WriteArray(writer, "disable", disable);
            }

            if (ignore is { Count: > 0 })
            {
                WriteArray(writer, "ignore", ignore);
            }

            if (comment)
            {
                writer.WriteBoolean("comment", true);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
    }

    private static void WriteArray(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }
}
