namespace Aibysitter.Web.GitHub;

public sealed record PullRequestRef(long InstallationId, string Owner, string Repo, int Number, string HeadSha)
{
    public override string ToString() => $"{Owner}/{Repo}#{Number}@{HeadSha[..Math.Min(7, HeadSha.Length)]}";
}
