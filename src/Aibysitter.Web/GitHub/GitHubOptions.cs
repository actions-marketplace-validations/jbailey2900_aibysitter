namespace Aibysitter.Web.GitHub;

/// <summary>Bound from the "GitHub" section. On IIS: GitHub__AppId, GitHub__WebhookSecret, GitHub__PrivateKeyPath.</summary>
public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    public const string CheckRunName = "Aibysitter";

    /// <summary>Numeric App ID or the Client ID (Iv…); GitHub accepts either as the JWT issuer.</summary>
    public string? AppId { get; set; }

    public string? WebhookSecret { get; set; }

    public string? PrivateKeyPath { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(WebhookSecret) && !string.IsNullOrWhiteSpace(PrivateKeyPath);
}
