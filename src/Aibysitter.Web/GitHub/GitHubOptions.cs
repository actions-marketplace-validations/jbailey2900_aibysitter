namespace Aibysitter.Web.GitHub;

/// <summary>Bound from the "GitHub" section. On IIS: GitHub__AppId, GitHub__WebhookSecret, GitHub__PrivateKeyPath.</summary>
public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    public const string CheckRunName = "Aibysitter";

    public long AppId { get; set; }

    public string? WebhookSecret { get; set; }

    public string? PrivateKeyPath { get; set; }

    public bool IsConfigured =>
        AppId > 0 && !string.IsNullOrWhiteSpace(WebhookSecret) && !string.IsNullOrWhiteSpace(PrivateKeyPath);
}
