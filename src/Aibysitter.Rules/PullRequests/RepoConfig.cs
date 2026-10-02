using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

public enum ConclusionMode
{
    Advisory,
    FailOnErrors,
}

/// <param name="Line">1-based line in the config file.</param>
public sealed record ConfigError(int Line, string Message)
{
    public override string ToString() => Message;
}

/// <summary>Parsed <c>.github/aibysitter.json</c>.</summary>
public sealed partial record RepoConfig(IReadOnlyList<Glob> Scope, ConclusionMode Conclusion)
{
    public const string FilePath = ".github/aibysitter.json";

    public static RepoConfig Default { get; } = new([], ConclusionMode.Advisory);

    /// <summary>Check IDs (Pnnn) and rule IDs (Rnnn) listed under <c>disable</c>. Disabled checks do not run; disabled rules are skipped by P014.</summary>
    public IReadOnlySet<string> Disabled { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Post one PR comment with the summary, updated in place (<c>"comment": true</c>).</summary>
    public bool Comment { get; init; }

    public bool HasScope => Scope.Count > 0;

    public bool IsEnabled(string checkId) => !Disabled.Contains(checkId);

    public bool InScope(string path) => Scope.Any(g => g.IsMatch(path));

    /// <summary>Parses config JSON. Invalid parts fall back to defaults and are reported in Errors.</summary>
    public static (RepoConfig Config, IReadOnlyList<ConfigError> Errors) Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (Default, []);
        }

        var errors = new List<ConfigError>();
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException ex)
        {
            return (Default, [new ConfigError((int)(ex.LineNumber ?? 0) + 1, $"{FilePath}: invalid JSON ({ex.Message})")]);
        }

        var lines = KeyLines(json);
        void Error(string key, string message) => errors.Add(new ConfigError(lines.GetValueOrDefault(key, 1), $"{FilePath}: {message}"));

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (Default, [new ConfigError(1, $"{FilePath}: root must be an object")]);
            }

            var scope = new List<Glob>();
            var conclusion = ConclusionMode.Advisory;
            var disabled = new HashSet<string>(StringComparer.Ordinal);
            var comment = false;

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "scope" when property.Value.ValueKind == JsonValueKind.Array:
                        var index = 0;
                        foreach (var item in property.Value.EnumerateArray())
                        {
                            var key = $"scope[{index++}]";
                            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
                            {
                                Error(key, "\"scope\" entries must be non-empty strings");
                            }
                            else if (Glob.TryCreate(item.GetString(), out var glob, out var globError))
                            {
                                scope.Add(glob!);
                            }
                            else
                            {
                                Error(key, $"scope entry {item.GetRawText()}: {globError}");
                            }
                        }

                        break;
                    case "scope":
                        Error("scope", "\"scope\" must be an array of path globs");
                        break;
                    case "conclusion":
                        switch (property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null)
                        {
                            case "advisory":
                                conclusion = ConclusionMode.Advisory;
                                break;
                            case "fail-on-errors":
                                conclusion = ConclusionMode.FailOnErrors;
                                break;
                            default:
                                Error("conclusion", "\"conclusion\" must be \"advisory\" or \"fail-on-errors\"");
                                break;
                        }

                        break;
                    case "disable" when property.Value.ValueKind == JsonValueKind.Array:
                        var disableIndex = 0;
                        foreach (var item in property.Value.EnumerateArray())
                        {
                            var key = $"disable[{disableIndex++}]";
                            var id = item.ValueKind == JsonValueKind.String ? item.GetString()?.Trim().ToUpperInvariant() : null;
                            if (id is not null && CheckIdRegex().IsMatch(id) && (PullRequestCheckDocs.Find(id) ?? RuleDocs.Find(id)) is not null)
                            {
                                disabled.Add(id);
                            }
                            else
                            {
                                Error(key, $"\"disable\" entry {item.GetRawText()} is not a known check or rule ID");
                            }
                        }

                        break;
                    case "disable":
                        Error("disable", "\"disable\" must be an array of check or rule IDs");
                        break;
                    case "comment" when property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                        comment = property.Value.GetBoolean();
                        break;
                    case "comment":
                        Error("comment", "\"comment\" must be true or false");
                        break;
                    default:
                        Error(property.Name, $"unknown key \"{property.Name}\"");
                        break;
                }
            }

            return (new RepoConfig(scope, conclusion) { Disabled = disabled, Comment = comment }, errors);
        }
    }

    /// <summary>1-based line of each top-level key (<c>scope</c>) and of each item in a top-level array (<c>scope[0]</c>).</summary>
    private static Dictionary<string, int> KeyLines(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var lineStarts = new List<long> { 0 };
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == (byte)'\n')
            {
                lineStarts.Add(i + 1);
            }
        }

        int LineAt(long offset)
        {
            var found = lineStarts.BinarySearch(offset);
            return (found >= 0 ? found : ~found - 1) + 1;
        }

        var lines = new Dictionary<string, int>(StringComparer.Ordinal);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        string? property = null;
        var index = 0;
        while (reader.Read())
        {
            if (reader.CurrentDepth == 1 && reader.TokenType == JsonTokenType.PropertyName)
            {
                property = reader.GetString();
                lines[property!] = LineAt(reader.TokenStartIndex);
                index = 0;
            }
            else if (reader.CurrentDepth == 2 && property is not null && reader.TokenType is not (JsonTokenType.EndArray or JsonTokenType.EndObject or JsonTokenType.PropertyName))
            {
                lines.TryAdd($"{property}[{index++}]", LineAt(reader.TokenStartIndex));
            }
        }

        return lines;
    }

    [GeneratedRegex(@"^[PR]\d{3}$")]
    private static partial Regex CheckIdRegex();
}
