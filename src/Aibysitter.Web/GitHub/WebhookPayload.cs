using System.Text.Json;

namespace Aibysitter.Web.GitHub;

public static class WebhookPayload
{
    public static readonly IReadOnlySet<string> ReviewedActions = new HashSet<string>(StringComparer.Ordinal) { "opened", "synchronize", "reopened" };

    /// <summary>Reads action and PR reference from a pull_request event body. Returns false when required fields are missing.</summary>
    public static bool TryParsePullRequest(ReadOnlySpan<byte> body, out string action, out PullRequestRef? pullRequest)
    {
        action = string.Empty;
        pullRequest = null;

        try
        {
            var reader = new Utf8JsonReader(body);
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            action = root.GetProperty("action").GetString() ?? string.Empty;
            pullRequest = new PullRequestRef(
                root.GetProperty("installation").GetProperty("id").GetInt64(),
                root.GetProperty("repository").GetProperty("owner").GetProperty("login").GetString()!,
                root.GetProperty("repository").GetProperty("name").GetString()!,
                root.GetProperty("pull_request").GetProperty("number").GetInt32(),
                root.GetProperty("pull_request").GetProperty("head").GetProperty("sha").GetString()!);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }
}
