using System.Text;
using System.Text.Json;
using Aibysitter.Web.GitHub;

namespace Aibysitter.Rules.Tests.GitHub;

internal static class WebhookTestData
{
    public const string Secret = "test-webhook-secret";

    public const string BaseSha = "fedcba9876543210fedcba9876543210fedcba98";

    public static string PullRequestPayload(string action = "opened", string sha = "0123456789abcdef0123456789abcdef01234567") =>
        JsonSerializer.Serialize(new
        {
            action,
            installation = new { id = 42 },
            repository = new { name = "sandbox", owner = new { login = "jbailey2900" } },
            pull_request = new { number = 7, head = new { sha }, @base = new { sha = BaseSha } },
        });

    public static HttpRequestMessage Request(string eventName, string json, string? signature = null, string? deliveryId = null)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var request = new HttpRequestMessage(HttpMethod.Post, GitHubWebhookEndpoint.Path)
        {
            Content = new ByteArrayContent(body),
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-GitHub-Event", eventName);
        request.Headers.Add("X-GitHub-Delivery", deliveryId ?? Guid.NewGuid().ToString());
        request.Headers.Add(WebhookSignature.HeaderName, signature ?? WebhookSignature.Compute(Secret, body));
        return request;
    }
}
