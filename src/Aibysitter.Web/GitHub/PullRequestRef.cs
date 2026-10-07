namespace Aibysitter.Web.GitHub;

/// <param name="BaseSha">Base branch commit from the webhook; null for jobs saved before it was recorded.</param>
public sealed record PullRequestRef(long InstallationId, string Owner, string Repo, int Number, string HeadSha, string? BaseSha = null)
{
    public override string ToString() => $"{Owner}/{Repo}#{Number}@{HeadSha[..Math.Min(7, HeadSha.Length)]}";
}
