using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aibysitter.Rules.PullRequests;

public enum ConclusionMode
{
    Advisory,
    FailOnErrors,
}

/// <summary>Parsed <c>.github/aibysitter.json</c>.</summary>
public sealed partial record RepoConfig(IReadOnlyList<Glob> Scope, ConclusionMode Conclusion)
{
    public const string FilePath = ".github/aibysitter.json";

    public static RepoConfig Default { get; } = new([], ConclusionMode.Advisory);

    /// <summary>Check IDs (Pnnn) and rule IDs (Rnnn) listed under <c>disable</c>. Disabled checks do not run; disabled rules are skipped by P014.</summary>
    public IReadOnlySet<string> Disabled { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    public bool HasScope => Scope.Count > 0;

    public bool IsEnabled(string checkId) => !Disabled.Contains(checkId);

    public bool InScope(string path) => Scope.Any(g => g.IsMatch(path));

    /// <summary>Parses config JSON. Invalid parts fall back to defaults and are reported in Errors.</summary>
    public static (RepoConfig Config, IReadOnlyList<string> Errors) Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (Default, []);
        }

        var errors = new List<string>();
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException ex)
        {
            return (Default, [$"{FilePath}: invalid JSON ({ex.Message})"]);
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (Default, [$"{FilePath}: root must be an object"]);
            }

            var scope = new List<Glob>();
            var conclusion = ConclusionMode.Advisory;
            var disabled = new HashSet<string>(StringComparer.Ordinal);

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "scope" when property.Value.ValueKind == JsonValueKind.Array:
                        foreach (var item in property.Value.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                            {
                                scope.Add(new Glob(item.GetString()!));
                            }
                            else
                            {
                                errors.Add($"{FilePath}: \"scope\" entries must be non-empty strings");
                            }
                        }

                        break;
                    case "scope":
                        errors.Add($"{FilePath}: \"scope\" must be an array of path globs");
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
                                errors.Add($"{FilePath}: \"conclusion\" must be \"advisory\" or \"fail-on-errors\"");
                                break;
                        }

                        break;
                    case "disable" when property.Value.ValueKind == JsonValueKind.Array:
                        foreach (var item in property.Value.EnumerateArray())
                        {
                            var id = item.ValueKind == JsonValueKind.String ? item.GetString()?.Trim().ToUpperInvariant() : null;
                            if (id is not null && CheckIdRegex().IsMatch(id) && (PullRequestCheckDocs.Find(id) ?? RuleDocs.Find(id)) is not null)
                            {
                                disabled.Add(id);
                            }
                            else
                            {
                                errors.Add($"{FilePath}: \"disable\" entry {item.GetRawText()} is not a known check or rule ID");
                            }
                        }

                        break;
                    case "disable":
                        errors.Add($"{FilePath}: \"disable\" must be an array of check or rule IDs");
                        break;
                    default:
                        errors.Add($"{FilePath}: unknown key \"{property.Name}\"");
                        break;
                }
            }

            return (new RepoConfig(scope, conclusion) { Disabled = disabled }, errors);
        }
    }

    [GeneratedRegex(@"^[PR]\d{3}$")]
    private static partial Regex CheckIdRegex();
}
